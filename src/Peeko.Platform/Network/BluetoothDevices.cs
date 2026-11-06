using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Peeko.Platform.Network;

public enum BluetoothKind
{
    Other,
    Audio,
    Phone,
    Computer,
    Input,
}

/// <summary>Perangkat Bluetooth yang sudah dipasangkan. Baterai null jika Windows tidak tahu.</summary>
public sealed record BluetoothItem(string Id, string Name, string Address, BluetoothKind Kind, bool Connected, int? Battery, bool CanToggle);

/// <summary>
/// Daftar perangkat Bluetooth yang sudah dipasangkan, statusnya, dan baterainya (dibaca saat daftar
/// dibuka saja). Sambung/putus hanya untuk perangkat audio (headset, speaker, earbuds): Windows tidak
/// punya API publik untuk itu, jadi Peeko memakai perintah "sekali sambung/putus" milik driver audio
/// Bluetooth Windows (KSPROPSETID_BtAudio), sama dengan tombol Sambungkan di Pengaturan Suara.
/// </summary>
public static class BluetoothDevices
{
    private const string IsConnectedKey = "System.Devices.Aep.IsConnected";
    private const string AddressKey = "System.Devices.Aep.DeviceAddress";
    private const string MajorClassKey = "System.Devices.Aep.Bluetooth.Cod.Major";
    private const string ContainerKey = "System.Devices.Aep.ContainerId";
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2"; // DEVPKEY_Bluetooth_Battery

    public static async Task<IReadOnlyList<BluetoothItem>> ListAsync()
    {
        var items = new Dictionary<string, BluetoothItem>();
        try
        {
            var audio = AudioFilterAddresses();
            string[] selectors =
            [
                BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
            ];
            foreach (var selector in selectors)
            {
                var found = await DeviceInformation.FindAllAsync(selector, [IsConnectedKey, AddressKey, MajorClassKey, ContainerKey],
                    DeviceInformationKind.AssociationEndpoint);
                foreach (var d in found)
                {
                    if (string.IsNullOrWhiteSpace(d.Name)) continue;
                    string address = (Prop(d, AddressKey) as string ?? "").Replace(":", "").ToUpperInvariant();
                    bool connected = Prop(d, IsConnectedKey) is true;
                    var kind = Prop(d, MajorClassKey) switch
                    {
                        byte b => FromMajor(b),
                        uint u => FromMajor((byte)u),
                        _ => BluetoothKind.Other,
                    };
                    bool canToggle = address.Length > 0 && audio.Contains(address);
                    if (canToggle) kind = BluetoothKind.Audio;
                    int? battery = Prop(d, ContainerKey) is Guid container ? await BatteryAsync(container) : null;

                    // Perangkat dual-mode muncul dua kali (klasik + LE): gabungkan, ambil status terbaik.
                    string key = address.Length > 0 ? address : d.Id;
                    if (items.TryGetValue(key, out var seen))
                    {
                        items[key] = seen with
                        {
                            Connected = seen.Connected || connected,
                            Battery = seen.Battery ?? battery,
                            CanToggle = seen.CanToggle || canToggle,
                        };
                        continue;
                    }
                    items[key] = new BluetoothItem(d.Id, d.Name, address, kind, connected, battery, canToggle);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ArgumentException)
        {
        }
        return items.Values
            .OrderByDescending(i => i.Connected)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static object? Prop(DeviceInformation d, string key) =>
        d.Properties.TryGetValue(key, out var v) ? v : null;

    private static BluetoothKind FromMajor(byte major) => major switch
    {
        1 => BluetoothKind.Computer,
        2 => BluetoothKind.Phone,
        4 => BluetoothKind.Audio,
        5 => BluetoothKind.Input,
        _ => BluetoothKind.Other,
    };

    private static async Task<int?> BatteryAsync(Guid container)
    {
        try
        {
            var nodes = await DeviceInformation.FindAllAsync($"System.Devices.ContainerId:=\"{{{container}}}\"", [BatteryKey],
                DeviceInformationKind.Device);
            foreach (var n in nodes)
                if (n.Properties.TryGetValue(BatteryKey, out var v) && v is byte b and <= 100) return b;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
        }
        return null;
    }

    // ── Sambung/putus perangkat audio ──────────────────────────────────────

    /// <returns>true jika perintah diterima driver. Status sebenarnya terlihat beberapa detik kemudian.</returns>
    public static bool SetAudioConnected(string address, bool connect)
    {
        bool sent = false;
        foreach (var filter in AudioFilters())
        {
            if (!filter.Address.Equals(address, StringComparison.OrdinalIgnoreCase)) continue;
            // Headset punya dua filter (musik A2DP dan panggilan HFP); cukup satu yang diterima.
            if (SendOneShot(filter.DeviceId, connect ? 0u : 1u)) sent = true;
        }
        return sent;
    }

    /// <summary>Alamat (tanpa titik dua, huruf besar) perangkat Bluetooth yang punya endpoint audio Windows.</summary>
    private static HashSet<string> AudioFilterAddresses() =>
        [.. AudioFilters().Select(f => f.Address)];

    /// <summary>
    /// Filter KS Bluetooth di balik tiap endpoint audio (juga yang sedang terputus). ID-nya berbentuk
    /// "…\\?\bthenum#{uuid}_…#…&amp;AABBCCDDEEFF_c00000000#…", jadi alamat perangkat bisa dibaca dari situ.
    /// </summary>
    private static List<(string DeviceId, string Address)> AudioFilters()
    {
        var list = new List<(string, string)>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active | DeviceState.Unplugged | DeviceState.Disabled))
            {
                using (endpoint)
                {
                    try
                    {
                        var topology = endpoint.DeviceTopology;
                        if (topology.ConnectorCount == 0) continue;
                        var filterId = topology.GetConnector(0).ConnectedToDeviceId;
                        if (filterId is null || !filterId.Contains("bthenum", StringComparison.OrdinalIgnoreCase)) continue;
                        var address = ParseAddress(filterId);
                        if (address is not null && !list.Any(f => f.Item1 == filterId)) list.Add((filterId, address));
                    }
                    catch (Exception ex) when (ex is COMException or InvalidCastException or NotImplementedException)
                    {
                    }
                }
            }
        }
        catch (COMException)
        {
        }
        return list;
    }

    internal static string? ParseAddress(string filterId)
    {
        // Cari 12 digit heksadesimal tepat sebelum "_c0…" atau di antara '&' dan '_'.
        var parts = filterId.Split('#', '&', '\\');
        foreach (var raw in parts)
        {
            var p = raw.Split('_')[0];
            if (p.Length == 12 && p.All(Uri.IsHexDigit) && raw.Length > 12 && raw[12] == '_') return p.ToUpperInvariant();
        }
        return null;
    }

    private static bool SendOneShot(string filterDeviceId, uint property)
    {
        IMMDeviceEnumeratorRaw? enumerator = null;
        IMMDeviceRaw? device = null;
        object? control = null;
        try
        {
            enumerator = (IMMDeviceEnumeratorRaw)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDevice(filterDeviceId, out device) != 0 || device is null) return false;
            var iid = typeof(IKsControl).GUID;
            if (device.Activate(ref iid, 0x17 /* CLSCTX_ALL */, IntPtr.Zero, out control) != 0 || control is not IKsControl ks) return false;
            var prop = new KsProperty { Set = new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D"), Id = property, Flags = 1 /* GET */ };
            int hr = ks.KsProperty(ref prop, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _);
            return hr >= 0;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return false;
        }
        finally
        {
            if (control is not null) Marshal.ReleaseComObject(control);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KsProperty
    {
        public Guid Set;
        public uint Id;
        public uint Flags;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject;

    // Hanya metode sampai GetDevice yang dipakai; urutannya harus sesuai vtable.
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumeratorRaw
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDeviceRaw device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceRaw
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IKsControl
    {
        [PreserveSig] int KsProperty(ref KsProperty property, uint propertyLength, IntPtr data, uint dataLength, out uint returned);
        [PreserveSig] int KsMethod(IntPtr method, uint methodLength, IntPtr data, uint dataLength, out uint returned);
        [PreserveSig] int KsEvent(IntPtr @event, uint eventLength, IntPtr data, uint dataLength, out uint returned);
    }
}
