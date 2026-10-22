// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): tangkapan loopback WASAPI
// untuk visualizer, ikut berpindah saat perangkat output default berganti.

using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Peeko.Core.Media;

namespace Peeko.Platform.Audio;

/// <summary>
/// Mendengarkan suara yang sedang diputar (loopback perangkat output default) dan mengubahnya menjadi
/// 5 level pita frekuensi. Hanya berjalan di antara <see cref="Start"/> dan <see cref="Stop"/>, jadi
/// saat musik dijeda atau pil tak terlihat tidak ada pemrosesan audio sama sekali.
/// </summary>
public sealed class AudioSpectrum : IDisposable
{
    private static readonly TimeSpan DeviceCheck = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(2.5);
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly object _gate = new();
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly Timer _watchdog;
    private WasapiLoopbackCapture? _capture;
    private string? _deviceId;
    private float[] _mono = [];
    private float[] _levels = Enumerable.Repeat(SpectrumAnalyzer.Floor, SpectrumAnalyzer.BandCount).ToArray();
    private bool _wanted;

    public AudioSpectrum()
    {
        _watchdog = new Timer(_ => CheckDevice(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Level terbaru (0.18–1) per pita; aman dibaca dari thread mana pun.</summary>
    public float[] Levels => Volatile.Read(ref _levels);

    public void Start()
    {
        lock (_gate)
        {
            if (_wanted) return;
            _wanted = true;
            _analyzer.Reset();
            Open();
            _watchdog.Change(DeviceCheck, DeviceCheck);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_wanted) return;
            _wanted = false;
            _watchdog.Change(Timeout.Infinite, Timeout.Infinite);
            Close();
            Volatile.Write(ref _levels, Enumerable.Repeat(SpectrumAnalyzer.Floor, SpectrumAnalyzer.BandCount).ToArray());
        }
    }

    private void Open()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _deviceId = device.ID;
            _capture = new WasapiLoopbackCapture(device);
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
            _capture.StartRecording();
        }
        catch (COMException)
        {
            // Tidak ada perangkat output; watchdog mencoba lagi.
            Close();
        }
    }

    private void Close()
    {
        var capture = _capture;
        _capture = null;
        _deviceId = null;
        if (capture is null) return;
        capture.DataAvailable -= OnData;
        capture.RecordingStopped -= OnStopped;
        try { capture.StopRecording(); } catch (COMException) { }
        capture.Dispose();
    }

    /// <summary>Headphone dicolok/dicabut: tangkapan pindah ke perangkat default yang baru.</summary>
    private void CheckDevice()
    {
        lock (_gate)
        {
            if (!_wanted) return;
            string? current = null;
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            }
            catch (COMException) { }
            if (_capture is not null && current == _deviceId) return;
            Close();
            if (current is not null) Open();
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is null) return;
        // Perangkat hilang di tengah jalan: tutup, lalu watchdog membuka lagi setelah jeda.
        lock (_gate)
        {
            if (sender == _capture) Close();
            if (_wanted) _watchdog.Change(Retry, DeviceCheck);
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture || e.BytesRecorded == 0) return;
        var format = capture.WaveFormat;
        int frames = ToMono(e.Buffer, e.BytesRecorded, format);
        if (frames > 0 && _analyzer.Push(_mono.AsSpan(0, frames)))
            Volatile.Write(ref _levels, _analyzer.Levels);
    }

    /// <summary>Rata-rata semua kanal per frame; mendukung float 32-bit dan PCM 16/24/32-bit.</summary>
    private int ToMono(byte[] buffer, int bytes, WaveFormat format)
    {
        int channels = Math.Max(1, format.Channels);
        int sampleBytes = format.BitsPerSample / 8;
        if (sampleBytes is < 2 or > 4) return 0;
        int frames = bytes / (sampleBytes * channels);
        if (_mono.Length < frames) _mono = new float[frames];

        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
            || (format is WaveFormatExtensible ext && ext.SubFormat == IeeeFloatSubFormat);

        var span = buffer.AsSpan(0, frames * sampleBytes * channels);
        for (int f = 0; f < frames; f++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++)
            {
                var s = span.Slice((f * channels + c) * sampleBytes, sampleBytes);
                sum += (isFloat, sampleBytes) switch
                {
                    (true, 4) => BitConverter.ToSingle(s),
                    (false, 2) => BitConverter.ToInt16(s) / 32768f,
                    (false, 3) => ((s[2] << 24) | (s[1] << 16) | (s[0] << 8)) / 2147483648f,
                    (false, 4) => BitConverter.ToInt32(s) / 2147483648f,
                    _ => 0,
                };
            }
            _mono[f] = sum / channels;
        }
        return frames;
    }

    public void Dispose()
    {
        Stop();
        _watchdog.Dispose();
    }
}
