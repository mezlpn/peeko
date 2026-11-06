using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Peeko.Platform.Network;

/// <summary>Permintaan izin dari Claude Code (hook PermissionRequest).</summary>
public sealed record ClaudePermission(string Tool, string Summary, string Folder);

/// <summary>Jawaban dari notch. Null = tidak dijawab (Claude Code menampilkan dialognya sendiri).</summary>
public enum ClaudeDecision
{
    Allow,
    Deny,
}

/// <summary>
/// Jembatan Claude Code ↔ Peeko. Server HTTP kecil yang hanya mendengar di 127.0.0.1 dan hanya
/// menerima permintaan dengan token Peeko (dipasang di perintah hook). Hook Claude Code memanggilnya
/// dengan curl: kabar (Notification/Stop) langsung dibalas kosong; permintaan izin menunggu jawaban
/// dari notch paling lama <see cref="AnswerTimeout"/>, lalu menyerah supaya dialog biasa muncul.
/// </summary>
public sealed class ClaudeBridge : IDisposable
{
    public const int Port = 47821;
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(25);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string _token = "";

    public bool IsRunning => _listener is not null;

    /// <summary>(event hook, pesan, folder proyek). Dari thread latar.</summary>
    public event Action<string, string, string>? Notified;

    /// <summary>Minta jawaban; kembalikan null kalau tidak dijawab. Dipanggil dari thread latar.</summary>
    public Func<ClaudePermission, CancellationToken, Task<ClaudeDecision?>>? PermissionHandler { get; set; }

    public bool Start(string token)
    {
        if (IsRunning) return true;
        _token = token;
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
        }
        catch (SocketException)
        {
            _listener = null; // port dipakai aplikasi lain
            return false;
        }
        _cts = new CancellationTokenSource();
        _ = AcceptLoop(_listener, _cts.Token);
        return true;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
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
            var stream = client.GetStream();
            try
            {
                var (path, headers, body) = await ReadRequestAsync(stream, ct);
                if (path is null) return;
                if (!headers.TryGetValue("x-peeko", out var token) || token != _token)
                {
                    await RespondAsync(stream, 403, "", ct);
                    return;
                }
                body = body.TrimStart('﻿').Trim();
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(body.Length == 0 ? "{}" : body);
                }
                catch (JsonException)
                {
                    await RespondAsync(stream, 400, "", ct);
                    return;
                }
                using var _ = doc;
                var root = doc.RootElement;
                string folder = Str(root, "cwd") is { Length: > 0 } cwd ? Path.GetFileName(cwd.TrimEnd('\\', '/')) : "";
                if (path.StartsWith("/claude/notify"))
                {
                    string ev = Str(root, "hook_event_name") ?? "";
                    Notified?.Invoke(ev, Str(root, "message") ?? "", folder);
                    await RespondAsync(stream, 200, "", ct);
                    return;
                }
                if (path.StartsWith("/claude/permission") && PermissionHandler is { } handler)
                {
                    string tool = Str(root, "tool_name") ?? "alat";
                    var request = new ClaudePermission(tool, Summarize(tool, root), folder);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(AnswerTimeout);
                    ClaudeDecision? decision;
                    try
                    {
                        decision = await handler(request, timeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        decision = null;
                    }
                    await RespondAsync(stream, 200, decision switch
                    {
                        ClaudeDecision.Allow => """{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"}}}""",
                        ClaudeDecision.Deny => """{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"deny","message":"Ditolak dari Peeko"}}}""",
                        _ => "", // tidak dijawab: Claude Code menampilkan dialog biasa
                    }, ct);
                    return;
                }
                await RespondAsync(stream, 404, "", ct);
            }
            catch (Exception ex) when (ex is IOException or SocketException or JsonException or OperationCanceledException)
            {
            }
        }
    }

    /// <summary>Ringkasan satu baris: perintah Bash, path file, atau alamat web.</summary>
    internal static string Summarize(string tool, JsonElement root)
    {
        if (!root.TryGetProperty("tool_input", out var input) || input.ValueKind != JsonValueKind.Object) return tool;
        string? s = Str(input, "command") ?? Str(input, "file_path") ?? Str(input, "path") ?? Str(input, "url") ?? Str(input, "pattern")
                    ?? Str(input, "description");
        if (s is null) return tool;
        s = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return s.Length > 160 ? s[..160] + "…" : s;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static async Task<(string? Path, Dictionary<string, string> Headers, string Body)> ReadRequestAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int headEnd = -1;
        while (headEnd < 0)
        {
            int n = await stream.ReadAsync(chunk, ct);
            if (n == 0) return (null, [], "");
            buffer.Write(chunk, 0, n);
            headEnd = buffer.GetBuffer().AsSpan(0, (int)buffer.Length).IndexOf("\r\n\r\n"u8);
            if (buffer.Length > 64 * 1024 && headEnd < 0) return (null, [], "");
        }
        var all = buffer.ToArray();
        var head = Encoding.ASCII.GetString(all, 0, headEnd).Split("\r\n");
        var first = head[0].Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in head.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        int length = headers.TryGetValue("content-length", out var l) && int.TryParse(l, out var len) ? Math.Min(len, 1024 * 1024) : 0;
        var body = new MemoryStream();
        body.Write(all, headEnd + 4, all.Length - headEnd - 4);
        while (body.Length < length)
        {
            int n = await stream.ReadAsync(chunk, ct);
            if (n == 0) break;
            body.Write(chunk, 0, n);
        }
        return (first.Length > 1 ? first[1] : null, headers, Encoding.UTF8.GetString(body.ToArray(), 0, (int)Math.Min(body.Length, length)));
    }

    private static async Task RespondAsync(NetworkStream stream, int status, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
        await stream.WriteAsync(bytes, ct);
    }

    public void Dispose() => Stop();
}
