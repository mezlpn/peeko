// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): setup_audio_visualization,
// src-tauri/src/services.rs. FFT 512 titik → 5 pita, penguatan otomatis per pita, penghalusan naik/turun.

namespace Peeko.Core.Media;

/// <summary>
/// Mengubah sampel audio mono menjadi 5 level pita frekuensi (0.18–1) untuk visualizer.
/// Tidak thread-safe; panggil dari satu thread (thread tangkapan audio).
/// </summary>
public sealed class SpectrumAnalyzer
{
    public const int FftSize = 512;
    public const int BandCount = 5;
    public const float Floor = 0.18f;

    // Rentang bin FFT tiap pita; pada 48 kHz ≈ 94–188 Hz, 188–563, 563–1.7k, 1.7k–5.6k, 5.6k–18.8k.
    private static readonly (int Start, int End)[] Bands = [(1, 2), (2, 6), (6, 18), (18, 60), (60, 200)];
    // Pita tinggi energinya kecil; dikuatkan supaya kelima bar sama-sama hidup.
    private static readonly float[] Weighting = [1.2f, 1.2f, 1.5f, 2.8f, 5.0f];

    private readonly float[] _window = new float[FftSize];
    private readonly float[] _buffer = new float[FftSize];
    private readonly float[] _re = new float[FftSize];
    private readonly float[] _im = new float[FftSize];
    private readonly float[] _maxEnergy = new float[BandCount];
    private readonly float[] _levels = new float[BandCount];
    private int _filled;

    public SpectrumAnalyzer()
    {
        for (int i = 0; i < FftSize; i++)
            _window[i] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * i / FftSize));
        Reset();
    }

    /// <summary>Level terakhir tiap pita (salinan).</summary>
    public float[] Levels => (float[])_levels.Clone();

    public void Reset()
    {
        _filled = 0;
        Array.Fill(_maxEnergy, 0.01f);
        Array.Fill(_levels, Floor);
    }

    /// <summary>Tambah sampel; mengembalikan true jika level pita baru saja diperbarui.</summary>
    public bool Push(ReadOnlySpan<float> mono)
    {
        bool updated = false;
        foreach (float sample in mono)
        {
            _buffer[_filled] = sample * _window[_filled];
            if (++_filled < FftSize) continue;
            _filled = 0;
            Analyze();
            updated = true;
        }
        return updated;
    }

    private void Analyze()
    {
        Array.Copy(_buffer, _re, FftSize);
        Array.Clear(_im);
        Fft(_re, _im);

        for (int band = 0; band < BandCount; band++)
        {
            var (start, end) = Bands[band];
            float total = 0;
            int count = 0;
            for (int bin = start; bin < end && bin < FftSize / 2; bin++)
            {
                total += MathF.Sqrt(_re[bin] * _re[bin] + _im[bin] * _im[bin]);
                count++;
            }
            float energy = total / Math.Max(1, count) * Weighting[band];

            if (energy > _maxEnergy[band]) _maxEnergy[band] = energy;
            else _maxEnergy[band] *= 0.99f;

            float target = MathF.Pow(Math.Min(1, energy / Math.Max(0.12f, _maxEnergy[band])), 0.75f);
            float smooth = target > _levels[band] ? 0.10f : 0.20f;
            _levels[band] = Math.Clamp(_levels[band] * smooth + target * (1 - smooth), Floor, 1);
        }
    }

    /// <summary>FFT radix-2 in-place (Cooley–Tukey), panjang harus pangkat dua.</summary>
    internal static void Fft(float[] re, float[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            float angle = -2 * MathF.PI / len;
            float wr = MathF.Cos(angle), wi = MathF.Sin(angle);
            for (int i = 0; i < n; i += len)
            {
                float cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    float tr = re[b] * cr - im[b] * ci;
                    float ti = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti;
                    re[a] += tr; im[a] += ti;
                    float next = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = next;
                }
            }
        }
    }
}
