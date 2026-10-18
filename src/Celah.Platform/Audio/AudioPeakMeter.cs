using System.Runtime.InteropServices;

namespace Celah.Platform.Audio;

/// <summary>
/// Level puncak keluaran audio (0–1) dari perangkat output default, lewat Core Audio
/// <c>IAudioMeterInformation</c>. Dipakai visualizer (keputusan desain #6): murah, tanpa FFT.
/// </summary>
/// <remarks>Panggil dari satu thread (thread UI). Perangkat default diambil ulang jika berganti/gagal.</remarks>
public sealed class AudioPeakMeter : IDisposable
{
    private IAudioMeterInformation? _meter;
    private DateTime _nextRetry;

    public float GetPeak()
    {
        if (_meter is null && DateTime.UtcNow >= _nextRetry)
            _meter = TryOpenDefaultMeter();

        if (_meter is null) return 0;

        if (_meter.GetPeakValue(out var peak) == 0)
            return Math.Clamp(peak, 0, 1);

        // Perangkat dicabut/berganti: buka ulang di pemanggilan berikutnya.
        Release();
        return 0;
    }

    private IAudioMeterInformation? TryOpenDefaultMeter()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0)
                    return Fail();

                var iid = typeof(IAudioMeterInformation).GUID;
                int hr = device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out var obj);
                Marshal.ReleaseComObject(device);
                return hr == 0 ? (IAudioMeterInformation)obj : Fail();
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (COMException)
        {
            return Fail();
        }

        IAudioMeterInformation? Fail()
        {
            _nextRetry = DateTime.UtcNow.AddSeconds(5); // tidak ada perangkat output: jangan coba tiap frame
            return null;
        }
    }

    private void Release()
    {
        if (_meter is not null)
            Marshal.ReleaseComObject(_meter);
        _meter = null;
    }

    public void Dispose() => Release();

    // ── Interop Core Audio (hanya method yang dipakai; urutan vtable harus sesuai header) ──

    private enum EDataFlow { Render = 0 }

    private enum ERole { Multimedia = 1 }

    private static class ClsCtx
    {
        public const int All = 0x17;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }
}
