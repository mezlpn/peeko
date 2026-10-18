using System.Globalization;

namespace Celah.Core.Peeks;

/// <summary>
/// Memutuskan kapan status baterai layak ditampilkan. Tidak ada peek saat aplikasi baru mulai;
/// peringatan lemah/kritis masing-masing hanya sekali per siklus pengosongan.
/// </summary>
public sealed class BatteryPeekLogic(int lowThreshold = 20, int criticalThreshold = 10)
{
    private bool? _onAc;
    private bool _lowShown;
    private bool _criticalShown;

    public PeekRequest? Update(bool onAc, int percent, TimeSpan? remaining)
    {
        if (_onAc is null)
        {
            _onAc = onAc;
            // Sudah lemah saat aplikasi dibuka: jangan langsung mengomel.
            _lowShown = !onAc && percent <= lowThreshold;
            _criticalShown = !onAc && percent <= criticalThreshold;
            return null;
        }

        if (onAc != _onAc)
        {
            _onAc = onAc;
            if (onAc)
            {
                _lowShown = _criticalShown = false;
                return new PeekRequest(PeekKind.Charging) { Percent = percent };
            }
            return new PeekRequest(PeekKind.Discharging)
            {
                Percent = percent,
                Detail = remaining is { } r ? FormatRemaining(r) : null,
            };
        }

        if (onAc) return null;

        if (percent <= criticalThreshold && !_criticalShown)
        {
            _criticalShown = _lowShown = true;
            return new PeekRequest(PeekKind.BatteryCritical) { Percent = percent };
        }
        if (percent <= lowThreshold && !_lowShown)
        {
            _lowShown = true;
            return new PeekRequest(PeekKind.BatteryLow) { Percent = percent };
        }
        return null;
    }

    /// <summary>"±3 j 20 m" / "±45 m".</summary>
    public static string FormatRemaining(TimeSpan t)
    {
        int hours = (int)t.TotalHours;
        return hours > 0 ? $"±{hours} j {t.Minutes} m" : $"±{Math.Max(1, t.Minutes)} m";
    }
}

/// <summary>Peek Wi-Fi hanya saat jaringan Wi-Fi yang tersambung benar-benar berganti.</summary>
public sealed class WifiPeekLogic
{
    private bool _initialized;
    private string? _ssid;

    /// <param name="ssid">SSID yang tersambung, atau null jika tidak ada Wi-Fi.</param>
    public PeekRequest? Update(string? ssid)
    {
        if (!_initialized)
        {
            _initialized = true;
            _ssid = ssid;
            return null;
        }
        if (ssid == _ssid) return null;

        var previous = _ssid;
        _ssid = ssid;
        return ssid is not null
            ? new PeekRequest(PeekKind.WifiConnected) { Text = ssid }
            : new PeekRequest(PeekKind.WifiDisconnected) { Text = previous };
    }
}

/// <summary>Kode dan nama bahasa keyboard dari LCID (LOWORD dari HKL).</summary>
public static class KeyboardLanguage
{
    public static (string Code, string Name) FromLcid(int lcid)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(lcid);
            var code = culture.TwoLetterISOLanguageName.ToUpperInvariant();
            var name = culture.TwoLetterISOLanguageName switch
            {
                "id" => "Bahasa Indonesia",
                "ms" => "Bahasa Melayu",
                "jv" => "Basa Jawa",
                _ => Capitalize(CultureInfo.GetCultureInfo(culture.TwoLetterISOLanguageName).NativeName),
            };
            return (code, name);
        }
        catch (CultureNotFoundException)
        {
            return ($"{lcid:X4}", "Bahasa lain");
        }
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];
}
