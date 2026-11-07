using System.Windows;
using System.Windows.Media;
using Peeko.App.Settings;
using Peeko.Core.Settings;

namespace Peeko.App.Themes;

/// <summary>
/// Menerapkan warna aksen dan gaya latar notch dari Pengaturan. Brush di resource aplikasi selalu
/// dibekukan WPF, jadi tiap perubahan memasang brush baru; XAML memakainya lewat DynamicResource
/// sehingga ikut berubah tanpa restart. Brush yang diambil di kode memakai warna saat diambil.
/// </summary>
public static class ThemeService
{
    private static readonly string[] Keys =
    [
        "Peeko.Brush.Accent", "Peeko.Brush.AccentHover", "Peeko.Brush.AccentPressed",
        "Peeko.Brush.AccentMuted", "Peeko.Brush.AccentInk", "Peeko.Brush.NotchBg",
    ];

    public static void Attach(SettingsService settings)
    {
        Apply(settings.Current);
        settings.Changed += s => Application.Current.Dispatcher.BeginInvoke(() => Apply(s));
    }

    public static void Apply(AppSettings s)
    {
        var p = ThemePalette.Build(s.AccentColor, s.NotchStyle);
        Set(Keys[0], p.Accent);
        Set(Keys[1], p.Hover);
        Set(Keys[2], p.Pressed);
        Set(Keys[3], p.Muted);
        Set(Keys[4], p.Ink);
        Set(Keys[5], p.NotchBg);
    }

    private static void Set(string key, ThemePalette.Argb c)
    {
        var color = Color.FromArgb(c.A, c.R, c.G, c.B);
        var res = Application.Current.Resources;
        if (res[key] is SolidColorBrush current && current.Color == color) return;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        res[key] = brush;
    }
}
