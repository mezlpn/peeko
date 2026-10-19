namespace Peeko.Core.Media;

/// <summary>
/// Warna bar visualizer diambil dari warna dominan cover, dengan lightness minimal 65%
/// supaya terbaca di atas latar notch (Collapsed.dc.html).
/// </summary>
public static class CoverPalette
{
    public readonly record struct Rgb(byte R, byte G, byte B)
    {
        public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>Warna aksen "Nyala" sebagai cadangan.</summary>
    public static readonly Rgb Fallback = new(0xC6, 0xF3, 0x6B);

    /// <param name="bgra">Piksel 32-bit BGRA (format Bgra32/Pbgra32 WPF).</param>
    public static Rgb DominantColor(ReadOnlySpan<byte> bgra, double minLightness = 0.65)
    {
        // Histogram kasar 4 bit per kanal; bobot memihak warna jenuh supaya latar abu-abu
        // atau hitam/putih tidak mendominasi.
        var weights = new double[4096];
        var sumR = new double[4096];
        var sumG = new double[4096];
        var sumB = new double[4096];

        int pixels = bgra.Length / 4;
        int step = Math.Max(1, pixels / 4096); // cukup ±4000 sampel
        for (int i = 0; i < pixels; i += step)
        {
            int o = i * 4;
            byte b = bgra[o], g = bgra[o + 1], r = bgra[o + 2], a = bgra[o + 3];
            if (a < 128) continue;

            var (_, s, l) = ToHsl(r, g, b);
            if (l < 0.08 || l > 0.95) continue;

            double w = 0.15 + s * s;
            int bin = (r >> 4) << 8 | (g >> 4) << 4 | (b >> 4);
            weights[bin] += w;
            sumR[bin] += r * w;
            sumG[bin] += g * w;
            sumB[bin] += b * w;
        }

        int best = -1;
        double bestWeight = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] > bestWeight)
            {
                bestWeight = weights[i];
                best = i;
            }
        }
        if (best < 0) return Fallback;

        var color = new Rgb(
            (byte)(sumR[best] / bestWeight),
            (byte)(sumG[best] / bestWeight),
            (byte)(sumB[best] / bestWeight));

        var (h, sat, light) = ToHsl(color.R, color.G, color.B);
        if (sat < 0.12) return Fallback; // cover hitam-putih: pakai aksen
        return light >= minLightness ? color : FromHsl(h, sat, minLightness);
    }

    public static (double H, double S, double L) ToHsl(byte r8, byte g8, byte b8)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2;
        if (max == min) return (0, 0, l);

        double d = max - min;
        double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0)
                 : max == g ? (b - r) / d + 2
                 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    public static Rgb FromHsl(double h, double s, double l)
    {
        if (s == 0)
        {
            byte v = (byte)Math.Round(l * 255);
            return new(v, v, v);
        }

        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        return new(
            (byte)Math.Round(HueToRgb(p, q, h + 1.0 / 3) * 255),
            (byte)Math.Round(HueToRgb(p, q, h) * 255),
            (byte)Math.Round(HueToRgb(p, q, h - 1.0 / 3) * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
