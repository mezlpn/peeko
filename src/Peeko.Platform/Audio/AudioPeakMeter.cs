using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Peeko.Platform.Audio;

/// <summary>
/// Level puncak keluaran audio (0–1) dari perangkat output default, lewat Core Audio
/// <c>IAudioMeterInformation</c> (NAudio). Dipakai visualizer (keputusan desain #6): murah, tanpa FFT.
/// </summary>
/// <remarks>
/// Panggil dari satu thread (thread UI). Perangkat default diambil ulang jika berganti/gagal.
/// Memakai NAudio, bukan interop sendiri: dua definisi COM untuk CLSID yang sama membuat .NET
/// gagal meng-cast objek enumerator.
/// </remarks>
public sealed class AudioPeakMeter : IDisposable
{
    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(5);

    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private DateTime _nextRetry;
    private DateTime _nextDefaultCheck;

    public float GetPeak()
    {
        try
        {
            var now = DateTime.UtcNow;
            // Ikut perangkat default yang baru (mis. headphone dicolok) tanpa event tambahan.
            if (_device is not null && now >= _nextDefaultCheck)
            {
                _nextDefaultCheck = now + Recheck;
                var current = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (current.ID != _device.ID)
                {
                    _device.Dispose();
                    _device = current;
                }
                else
                {
                    current.Dispose();
                }
            }

            if (_device is null)
            {
                if (now < _nextRetry) return 0;
                _enumerator ??= new MMDeviceEnumerator();
                _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _nextDefaultCheck = now + Recheck;
            }

            return Math.Clamp(_device.AudioMeterInformation.MasterPeakValue, 0f, 1f);
        }
        catch (COMException)
        {
            // Tidak ada perangkat output, atau perangkat baru saja dicabut.
            _device?.Dispose();
            _device = null;
            _nextRetry = DateTime.UtcNow + Recheck;
            return 0;
        }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
        _enumerator?.Dispose();
        _enumerator = null;
    }
}
