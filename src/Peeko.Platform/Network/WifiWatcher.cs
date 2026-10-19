using Windows.Networking.Connectivity;

namespace Peeko.Platform.Network;

/// <summary>
/// SSID Wi-Fi yang tersambung, lewat <c>NetworkInformation.NetworkStatusChanged</c>. Event ini sering
/// datang beruntun saat jaringan berganti, jadi dibaca setelah stabil 1 detik.
/// </summary>
public sealed class WifiWatcher : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);
    private readonly Timer _settle;

    public WifiWatcher()
    {
        _settle = new Timer(_ => SsidChanged?.Invoke(ReadSsid()), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>SSID yang tersambung, atau null (dari thread latar). Dipanggil juga sekali saat mulai.</summary>
    public event Action<string?>? SsidChanged;

    public void Start()
    {
        NetworkInformation.NetworkStatusChanged += OnStatusChanged;
        SsidChanged?.Invoke(ReadSsid());
    }

    private void OnStatusChanged(object sender) => _settle.Change(Settle, Timeout.InfiniteTimeSpan);

    private static string? ReadSsid()
    {
        try
        {
            foreach (var profile in NetworkInformation.GetConnectionProfiles())
            {
                if (!profile.IsWlanConnectionProfile) continue;
                if (profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None) continue;
                // Di Windows 11 versi baru SSID butuh izin lokasi; nama profil biasanya sama dengan SSID.
                var ssid = profile.WlanConnectionProfileDetails?.GetConnectedSsid();
                return string.IsNullOrEmpty(ssid) ? profile.ProfileName : ssid;
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    public void Dispose()
    {
        NetworkInformation.NetworkStatusChanged -= OnStatusChanged;
        _settle.Dispose();
    }
}
