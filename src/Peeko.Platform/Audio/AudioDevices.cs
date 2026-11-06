using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Peeko.Platform.Audio;

public sealed record AudioDevice(string Id, string Name, bool IsDefault);

/// <summary>
/// Daftar speaker/mikrofon dan memilih perangkat default. Windows tidak punya API publik untuk
/// mengganti perangkat default; yang dipakai adalah IPolicyConfig, antarmuka COM yang juga dipakai
/// panel Suara Windows sendiri dan banyak aplikasi pengalih audio. Semua metode aman dipanggil dari
/// thread latar dan tidak melempar exception.
/// </summary>
public static class AudioDevices
{
    public static IReadOnlyList<AudioDevice> List(bool capture)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var flow = capture ? DataFlow.Capture : DataFlow.Render;
            string? defaultId = null;
            try
            {
                using var d = enumerator.GetDefaultAudioEndpoint(flow, capture ? Role.Communications : Role.Multimedia);
                defaultId = d.ID;
            }
            catch (COMException)
            {
            }
            var list = new List<AudioDevice>();
            foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                using (device) list.Add(new AudioDevice(device.ID, device.FriendlyName, device.ID == defaultId));
            return list;
        }
        catch (COMException)
        {
            return [];
        }
    }

    /// <summary>Jadikan perangkat default untuk semua peran (musik, game, panggilan).</summary>
    public static bool SetDefault(string id)
    {
        try
        {
            var config = (IPolicyConfig)new PolicyConfigClient();
            try
            {
                for (int role = 0; role < 3; role++)
                    Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(id, (Role)role));
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(config);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return false;
        }
    }

    /// <summary>Mikrofon default dibisukan? Null jika tidak ada mikrofon.</summary>
    public static bool? GetMicMuted()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return mic.AudioEndpointVolume.Mute;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static bool SetMicMuted(bool muted)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            mic.AudioEndpointVolume.Mute = muted;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient;

    // Urutan metode harus sama persis dengan vtable IPolicyConfig (Windows 7 ke atas).
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, bool @default, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, bool @default, IntPtr defaultPeriod, IntPtr minPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, bool visible);
    }
}
