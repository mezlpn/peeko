using System.Runtime.InteropServices;
using Windows.Devices.WiFi;
using Windows.Security.Credentials;

namespace Peeko.Platform.Network;

/// <summary>Satu jaringan Wi-Fi hasil pindai. Sinyal 0–4 batang.</summary>
public sealed record WifiNetwork(string Ssid, int Bars, bool Secured, bool Connected);

public enum WifiConnectResult
{
    Connected,
    NeedsPassword,
    WrongPassword,
    Failed,
}

/// <summary>
/// Memindai dan menyambung ke Wi-Fi lewat Windows.Devices.WiFi. Pindai hanya saat daftar dibuka
/// (tidak ada pemindaian di latar). Password tidak disimpan Peeko; Windows menyimpannya sebagai
/// profil Wi-Fi biasa, sama seperti menyambung dari taskbar.
/// </summary>
public sealed class WifiNetworks
{
    private WiFiAdapter? _adapter;
    private readonly Dictionary<string, WiFiAvailableNetwork> _found = [];

    /// <summary>Pindai ulang. Kosong jika tidak ada adapter Wi-Fi atau akses ditolak.</summary>
    public async Task<IReadOnlyList<WifiNetwork>> ScanAsync()
    {
        try
        {
            if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed) return [];
            _adapter ??= (await WiFiAdapter.FindAllAdaptersAsync()).FirstOrDefault();
            if (_adapter is null) return [];

            await _adapter.ScanAsync();
            string? connected = null;
            try
            {
                var profile = await _adapter.NetworkAdapter.GetConnectedProfileAsync();
                connected = profile?.WlanConnectionProfileDetails?.GetConnectedSsid();
            }
            catch (COMException)
            {
            }

            _found.Clear();
            foreach (var n in _adapter.NetworkReport.AvailableNetworks)
            {
                if (string.IsNullOrWhiteSpace(n.Ssid)) continue; // jaringan tersembunyi
                // SSID sama bisa muncul beberapa kali (2,4/5 GHz, beberapa access point): ambil yang terkuat.
                if (_found.TryGetValue(n.Ssid, out var seen) && seen.SignalBars >= n.SignalBars) continue;
                _found[n.Ssid] = n;
            }

            return _found.Values
                .Select(n => new WifiNetwork(n.Ssid, n.SignalBars,
                    n.SecuritySettings.NetworkAuthenticationType is not (Windows.Networking.Connectivity.NetworkAuthenticationType.Open80211
                        or Windows.Networking.Connectivity.NetworkAuthenticationType.None),
                    n.Ssid == connected))
                .OrderByDescending(n => n.Connected)
                .ThenByDescending(n => n.Bars)
                .ThenBy(n => n.Ssid, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Sambung ke jaringan dari pindaian terakhir. Tanpa password dicoba dengan profil yang sudah
    /// tersimpan di Windows; kalau belum ada, hasilnya <see cref="WifiConnectResult.NeedsPassword"/>.
    /// </summary>
    public async Task<WifiConnectResult> ConnectAsync(string ssid, string? password = null)
    {
        if (_adapter is null || !_found.TryGetValue(ssid, out var network)) return WifiConnectResult.Failed;
        try
        {
            WiFiConnectionResult result;
            if (password is null)
            {
                result = await _adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic);
            }
            else
            {
                var credential = new PasswordCredential { Password = password };
                result = await _adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic, credential);
            }
            return result.ConnectionStatus switch
            {
                WiFiConnectionStatus.Success => WifiConnectResult.Connected,
                WiFiConnectionStatus.InvalidCredential => password is null ? WifiConnectResult.NeedsPassword : WifiConnectResult.WrongPassword,
                _ => WifiConnectResult.Failed,
            };
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ArgumentException)
        {
            return WifiConnectResult.Failed;
        }
    }

    public void Disconnect()
    {
        try
        {
            _adapter?.Disconnect();
        }
        catch (COMException)
        {
        }
    }
}
