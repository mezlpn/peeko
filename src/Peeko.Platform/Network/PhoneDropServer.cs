using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Peeko.Platform.Network;

/// <summary>
/// "Kirim dari HP": server HTTP kecil di jaringan lokal yang hanya hidup selama panelnya dibuka.
/// HP yang tersambung ke Wi-Fi yang sama membuka alamatnya (lewat kode QR), lalu mengirim file atau
/// teks. Alamat memuat token acak, jadi perangkat lain di jaringan yang tidak melihat QR-nya tidak bisa
/// mengirim apa pun. Tidak ada yang keluar ke internet.
/// </summary>
public sealed class PhoneDropServer : IDisposable
{
    public const long MaxFileBytes = 4L * 1024 * 1024 * 1024; // 4 GB
    private const int MaxTextBytes = 1024 * 1024;

    private readonly string _saveDir;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string _token = "";

    public PhoneDropServer(string saveDir)
    {
        _saveDir = saveDir;
    }

    public string? Url { get; private set; }

    public bool IsRunning => _listener is not null;

    /// <summary>File tersimpan (path lengkap). Dari thread latar.</summary>
    public event Action<string>? FileReceived;

    /// <summary>Teks/tautan diterima. Dari thread latar.</summary>
    public event Action<string>? TextReceived;

    /// <summary>HP membuka halaman (untuk status "HP tersambung"). Dari thread latar.</summary>
    public event Action? Visited;

    /// <returns>Alamat untuk QR, atau null jika laptop tidak tersambung ke jaringan lokal.</returns>
    public string? Start()
    {
        if (IsRunning) return Url;
        var ip = LocalAddress();
        if (ip is null) return null;
        _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        _listener = new TcpListener(IPAddress.Any, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"http://{ip}:{port}/{_token}";
        _cts = new CancellationTokenSource();
        _ = AcceptLoop(_listener, _cts.Token);
        return Url;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        Url = null;
    }

    /// <summary>Alamat IPv4 jaringan yang punya gateway (Wi-Fi/LAN aktif), bukan adapter virtual.</summary>
    public static IPAddress? LocalAddress()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var desc = nic.Description.ToLowerInvariant();
                if (desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("hyper-v") || desc.Contains("vpn")) continue;
                var props = nic.GetIPProperties();
                if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) continue;
                var v4 = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (v4 is not null) return v4.Address;
            }
        }
        catch (NetworkInformationException)
        {
        }
        return null;
    }

    private async Task AcceptLoop(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            _ = Task.Run(() => HandleAsync(client, ct), ct);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.ReceiveTimeout = 30_000;
            var stream = client.GetStream();
            try
            {
                var (method, path, headers, leftover) = await ReadHeadAsync(stream, ct);
                if (method is null)
                {
                    return;
                }
                var (route, query) = SplitPath(path!);
                if (route == $"/{_token}" && method == "GET")
                {
                    Visited?.Invoke();
                    await RespondAsync(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page()), ct);
                    return;
                }
                if (route == $"/{_token}/file" && method == "POST")
                {
                    long length = headers.TryGetValue("content-length", out var l) && long.TryParse(l, out var n) ? n : -1;
                    if (length < 0 || length > MaxFileBytes)
                    {
                        await RespondAsync(stream, 413, "text/plain", "terlalu besar"u8.ToArray(), ct);
                        return;
                    }
                    var name = SafeName(query.GetValueOrDefault("name") ?? "file");
                    Directory.CreateDirectory(_saveDir);
                    var target = Unique(Path.Combine(_saveDir, name));
                    var partial = target + ".peeko-sebagian";
                    try
                    {
                        await using (var file = File.Create(partial))
                            await CopyBodyAsync(stream, leftover, file, length, ct);
                        File.Move(partial, target);
                    }
                    catch
                    {
                        try { File.Delete(partial); } catch (IOException) { }
                        throw;
                    }
                    FileReceived?.Invoke(target);
                    await RespondAsync(stream, 200, "text/plain", "ok"u8.ToArray(), ct);
                    return;
                }
                if (route == $"/{_token}/text" && method == "POST")
                {
                    long length = headers.TryGetValue("content-length", out var l) && long.TryParse(l, out var n) ? n : -1;
                    if (length is < 0 or > MaxTextBytes)
                    {
                        await RespondAsync(stream, 413, "text/plain", "terlalu panjang"u8.ToArray(), ct);
                        return;
                    }
                    using var ms = new MemoryStream();
                    await CopyBodyAsync(stream, leftover, ms, length, ct);
                    var text = Encoding.UTF8.GetString(ms.ToArray()).Trim();
                    if (text.Length > 0) TextReceived?.Invoke(text);
                    await RespondAsync(stream, 200, "text/plain", "ok"u8.ToArray(), ct);
                    return;
                }
                // Tanpa token yang benar: tidak ada apa-apa di sini.
                await RespondAsync(stream, 404, "text/plain", "tidak ditemukan"u8.ToArray(), ct);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task<(string? Method, string? Path, Dictionary<string, string> Headers, byte[] Leftover)> ReadHeadAsync(
        NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        int filled = 0;
        while (filled < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(filled), ct);
            if (read == 0) break;
            filled += read;
            int end = IndexOf(buffer, filled, "\r\n\r\n"u8);
            if (end < 0) continue;
            var head = Encoding.ASCII.GetString(buffer, 0, end).Split("\r\n");
            var first = head[0].Split(' ');
            if (first.Length < 2) break;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in head.Skip(1))
            {
                int colon = line.IndexOf(':');
                if (colon > 0) headers[line[..colon].Trim().ToLowerInvariant()] = line[(colon + 1)..].Trim();
            }
            var leftover = buffer[(end + 4)..filled];
            return (first[0], first[1], headers, leftover);
        }
        return (null, null, [], []);
    }

    private static int IndexOf(byte[] buffer, int length, ReadOnlySpan<byte> pattern) =>
        buffer.AsSpan(0, length).IndexOf(pattern);

    private static async Task CopyBodyAsync(NetworkStream stream, byte[] leftover, Stream target, long length, CancellationToken ct)
    {
        long remaining = length;
        int first = (int)Math.Min(leftover.Length, remaining);
        await target.WriteAsync(leftover.AsMemory(0, first), ct);
        remaining -= first;
        var buffer = new byte[81920];
        while (remaining > 0)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct);
            if (read == 0) throw new IOException("Koneksi terputus");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            remaining -= read;
        }
    }

    private static async Task RespondAsync(NetworkStream stream, int status, string type, byte[] body, CancellationToken ct)
    {
        var reason = status switch { 200 => "OK", 404 => "Not Found", 413 => "Payload Too Large", _ => "Error" };
        var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\n" +
                   "Cache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
        await stream.WriteAsync(body, ct);
    }

    internal static (string Route, Dictionary<string, string> Query) SplitPath(string path)
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int q = path.IndexOf('?');
        if (q < 0) return (path.TrimEnd('/'), query);
        foreach (var pair in path[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0) query[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
        }
        return (path[..q].TrimEnd('/'), query);
    }

    /// <summary>Nama file dari HP tanpa folder, karakter terlarang, atau nama perangkat Windows.</summary>
    public static string SafeName(string name)
    {
        name = Path.GetFileName(name.Replace('\\', '/').Split('/').Last());
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().Trim('.');
        if (name.Length == 0) name = "file";
        var stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
            name = "_" + name;
        return name.Length > 150 ? name[..150] : name;
    }

    private static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static string Page() => """
        <!doctype html><html lang="id"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Kirim ke Peeko</title>
        <style>
        :root{color-scheme:dark}
        body{margin:0;font-family:system-ui,-apple-system,"Segoe UI",sans-serif;background:#0b0b0d;color:#f2f2f4}
        main{max-width:460px;margin:0 auto;padding:28px 18px 40px}
        h1{font-size:22px;margin:0 0 4px}
        p{color:#a3a3ad;margin:0 0 22px;font-size:14px;line-height:1.5}
        .card{background:#16161a;border:1px solid #2a2a30;border-radius:18px;padding:16px;margin-bottom:14px}
        label{display:block;font-weight:600;margin-bottom:10px}
        input[type=file]{width:100%;color:#a3a3ad}
        textarea{width:100%;box-sizing:border-box;min-height:96px;background:#0b0b0d;color:#f2f2f4;border:1px solid #2a2a30;border-radius:12px;padding:10px;font:inherit;resize:vertical}
        button{margin-top:12px;width:100%;border:0;border-radius:999px;padding:13px;font:600 15px system-ui;background:#c6f36b;color:#11140a}
        button:disabled{opacity:.5}
        .bar{height:6px;background:#2a2a30;border-radius:3px;overflow:hidden;margin-top:12px;display:none}
        .bar i{display:block;height:100%;width:0;background:#c6f36b}
        #log{font-size:14px;color:#a3a3ad;margin-top:8px;min-height:20px}
        .ok{color:#c6f36b}
        </style></head><body><main>
        <h1>Kirim ke Peeko</h1>
        <p>File dan teks langsung muncul di Rak laptop. Hanya lewat Wi-Fi ini, tidak lewat internet.</p>
        <div class="card"><label for="f">File atau foto</label>
        <input id="f" type="file" multiple><button id="sf">Kirim file</button>
        <div class="bar" id="bar"><i id="fill"></i></div></div>
        <div class="card"><label for="t">Teks atau tautan</label>
        <textarea id="t" placeholder="Tempel di sini…"></textarea><button id="st">Kirim teks</button></div>
        <div id="log"></div>
        </main><script>
        const base=location.pathname.replace(/\/$/,''),log=document.getElementById('log');
        function say(m,ok){log.textContent=m;log.className=ok?'ok':''}
        function send(url,body,onp){return new Promise((res,rej)=>{const x=new XMLHttpRequest();x.open('POST',url);
        x.upload.onprogress=e=>{if(e.lengthComputable&&onp)onp(e.loaded/e.total)};
        x.onload=()=>x.status==200?res():rej(x.responseText||x.status);x.onerror=()=>rej('koneksi terputus');x.send(body)})}
        document.getElementById('sf').onclick=async()=>{const files=[...document.getElementById('f').files];if(!files.length){say('Pilih file dulu');return}
        const b=document.getElementById('sf'),bar=document.getElementById('bar'),fill=document.getElementById('fill');b.disabled=true;bar.style.display='block';
        try{for(let i=0;i<files.length;i++){const f=files[i];say('Mengirim '+(i+1)+'/'+files.length+': '+f.name);
        await send(base+'/file?name='+encodeURIComponent(f.name),f,p=>fill.style.width=Math.round(((i+p)/files.length)*100)+'%')}
        fill.style.width='100%';say(files.length+' file terkirim ✓',true);document.getElementById('f').value=''}
        catch(e){say('Gagal: '+e)}finally{b.disabled=false}};
        document.getElementById('st').onclick=async()=>{const t=document.getElementById('t');if(!t.value.trim()){say('Tulis sesuatu dulu');return}
        try{await send(base+'/text',t.value);say('Teks terkirim ✓',true);t.value=''}catch(e){say('Gagal: '+e)}};
        </script></body></html>
        """;

    public void Dispose() => Stop();
}
