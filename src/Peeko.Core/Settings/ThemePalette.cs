using Peeko.Core.Media;

namespace Peeko.Core.Settings;

/// <summary>Gaya latar notch.</summary>
public enum NotchStyle
{
    /// <summary>Hitam pekat (bawaan).</summary>
    Black,
    /// <summary>Hitam agak tembus pandang, desktop sedikit terlihat.</summary>
    Glass,
    /// <summary>Hitam dengan rona tipis warna aksen.</summary>
    Tinted,
}

/// <summary>
/// Turunan warna aksen dari satu warna pilihan. Rumusnya dicocokkan dengan token asli
/// (Tokens.xaml): hover = 20% ke putih, pressed = 15% ke hitam, muted = 14% aksen di atas latar notch.
/// </summary>
public static class ThemePalette
{
    public readonly record struct Argb(byte A, byte R, byte G, byte B)
    {
        public static Argb Opaque(CoverPalette.Rgb c) => new(255, c.R, c.G, c.B);
    }

    public sealed record Palette(Argb Accent, Argb Hover, Argb Pressed, Argb Muted, Argb Ink, Argb NotchBg);

    public static readonly CoverPalette.Rgb DefaultAccent = new(0xC6, 0xF3, 0x6B);
    private static readonly CoverPalette.Rgb Bg = new(0x05, 0x05, 0x06);

    /// <summary>Pilihan aksen di Pengaturan (nama, hex).</summary>
    public static readonly IReadOnlyList<(string Name, string Hex)> Presets =
    [
        ("Limau", "#C6F36B"),
        ("Langit", "#6CC8F2"),
        ("Lavender", "#B49CFF"),
        ("Sakura", "#FF9CC8"),
        ("Jingga", "#FFB066"),
        ("Mint", "#5FE3B5"),
        ("Putih", "#E8E8EC"),
    ];

    public static CoverPalette.Rgb ParseOrDefault(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return DefaultAccent;
        var h = hex.Trim().TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out int v)) return DefaultAccent;
        return new((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static Palette Build(string? accentHex, NotchStyle style)
    {
        var a = ParseOrDefault(accentHex);
        var white = new CoverPalette.Rgb(255, 255, 255);
        var black = new CoverPalette.Rgb(0, 0, 0);
        double lum = (0.2126 * a.R + 0.7152 * a.G + 0.0722 * a.B) / 255;
        var ink = lum > 0.45 ? new CoverPalette.Rgb(0x0B, 0x0F, 0x04) : white;
        var bg = style switch
        {
            NotchStyle.Glass => new Argb(0xD6, Bg.R, Bg.G, Bg.B),
            NotchStyle.Tinted => Argb.Opaque(Mix(Bg, a, 0.07)),
            _ => Argb.Opaque(Bg),
        };
        return new Palette(
            Argb.Opaque(a),
            Argb.Opaque(Mix(a, white, 0.2)),
            Argb.Opaque(Mix(a, black, 0.15)),
            Argb.Opaque(Mix(Bg, a, 0.14)),
            Argb.Opaque(ink),
            bg);
    }

    /// <summary>Campur <paramref name="from"/> ke <paramref name="to"/> sebanyak <paramref name="t"/> (0…1).</summary>
    public static CoverPalette.Rgb Mix(CoverPalette.Rgb from, CoverPalette.Rgb to, double t) => new(
        (byte)Math.Round(from.R + (to.R - from.R) * t),
        (byte)Math.Round(from.G + (to.G - from.G) * t),
        (byte)Math.Round(from.B + (to.B - from.B) * t));
}
