using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Celah.Platform.Audio;

/// <summary>
/// Volume/mute perangkat output default dan pergantian perangkat (headphone/Bluetooth masuk-keluar),
/// lewat Core Audio (NAudio). Semua objek COM dibuat dan dipakai di thread MTA; event dikirim dari
/// thread latar.
/// </summary>
public sealed class AudioDeviceWatcher : IDisposable
{
    // Windows mengirim beberapa event beruntun saat perangkat berganti; kumpulkan dulu sebentar.
    private static readonly TimeSpan DeviceSettle = TimeSpan.FromMilliseconds(400);

    private readonly object _gate = new();
    private readonly NotificationClient _client;
    private readonly Timer _settleTimer;
    private readonly Dictionary<string, string> _names = [];

    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private string? _defaultId;
    private float _lastVolume = -1;
    private bool _lastMuted;

    private bool _pendingDefaultChange;
    private string? _pendingDisconnected;

    public AudioDeviceWatcher()
    {
        _client = new NotificationClient(this);
        _settleTimer = new Timer(_ => FlushDeviceEvents(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>(level 0–1, bisu)</summary>
    public event Action<float, bool>? VolumeChanged;

    /// <summary>Perangkat output default berganti: (nama, headphone?)</summary>
    public event Action<string, bool>? DeviceConnected;

    /// <summary>Perangkat output terputus/dicabut: nama.</summary>
    public event Action<string>? DeviceDisconnected;

    public Task StartAsync() => Task.Run(() =>
    {
        lock (_gate)
        {
            _enumerator = new MMDeviceEnumerator();
            foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                _names[d.ID] = d.FriendlyName;
            _enumerator.RegisterEndpointNotificationCallback(_client);
            AttachDefault();
        }
    });

    private void AttachDefault()
    {
        if (_device is not null)
        {
            _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume;
            _device.Dispose();
            _device = null;
        }
        try
        {
            _device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _defaultId = _device.ID;
            _names[_device.ID] = _device.FriendlyName;
            _lastVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
            _lastMuted = _device.AudioEndpointVolume.Mute;
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolume;
        }
        catch (COMException)
        {
            _defaultId = null; // tidak ada perangkat output
        }
    }

    private void OnVolume(AudioVolumeNotificationData data)
    {
        // Abaikan notifikasi yang nilainya sama (beberapa driver mengirim ulang).
        if (Math.Abs(data.MasterVolume - _lastVolume) < 0.001f && data.Muted == _lastMuted) return;
        _lastVolume = data.MasterVolume;
        _lastMuted = data.Muted;
        VolumeChanged?.Invoke(data.MasterVolume, data.Muted);
    }

    private void OnDefaultChanged()
    {
        lock (_gate)
        {
            _pendingDefaultChange = true;
            _settleTimer.Change(DeviceSettle, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnStateChanged(string deviceId, DeviceState state)
    {
        lock (_gate)
        {
            if (state == DeviceState.Active)
            {
                _settleTimer.Change(DeviceSettle, Timeout.InfiniteTimeSpan);
                return;
            }
            // Hanya perangkat yang sedang dipakai; audio HDMI yang mati saat monitor tidur tidak perlu dikabarkan.
            if (deviceId == _defaultId && _names.TryGetValue(deviceId, out var name))
            {
                _pendingDisconnected = name;
                _settleTimer.Change(DeviceSettle, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Jika ada perangkat yang terputus, tampilkan itu saja (Windows otomatis pindah ke speaker,
    /// tapi yang penting bagi pengguna adalah headphone-nya lepas). Selain itu, laporkan perangkat
    /// default yang baru.
    /// </summary>
    private void FlushDeviceEvents()
    {
        string? disconnected;
        bool defaultChanged;
        string? connectedName = null;
        bool headphone = false;

        lock (_gate)
        {
            if (_enumerator is null) return;
            disconnected = _pendingDisconnected;
            defaultChanged = _pendingDefaultChange;
            _pendingDisconnected = null;
            _pendingDefaultChange = false;

            var previous = _defaultId;
            AttachDefault();
            if (defaultChanged && _device is not null && _defaultId != previous)
            {
                connectedName = _device.FriendlyName;
                headphone = IsHeadphone(_device);
            }
            else
            {
                defaultChanged = false;
            }
        }

        if (disconnected is not null) DeviceDisconnected?.Invoke(disconnected);
        else if (defaultChanged && connectedName is not null) DeviceConnected?.Invoke(connectedName, headphone);
    }

    private static bool IsHeadphone(MMDevice device)
    {
        try
        {
            // PKEY_AudioEndpoint_FormFactor: 3 = Headphones, 5 = Headset.
            if (device.Properties.Contains(PropertyKeys.PKEY_AudioEndpoint_FormFactor)
                && device.Properties[PropertyKeys.PKEY_AudioEndpoint_FormFactor].Value is uint form
                && form is 3 or 5)
                return true;
        }
        catch (COMException)
        {
        }
        var name = device.FriendlyName.ToLowerInvariant();
        return name.Contains("headphone") || name.Contains("headset") || name.Contains("buds")
            || name.Contains("airpods") || name.Contains("earphone");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _settleTimer.Dispose();
            if (_enumerator is not null)
            {
                try { _enumerator.UnregisterEndpointNotificationCallback(_client); } catch (COMException) { }
            }
            if (_device is not null)
            {
                _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume;
                _device.Dispose();
            }
            _enumerator?.Dispose();
            _enumerator = null;
        }
    }

    private sealed class NotificationClient(AudioDeviceWatcher owner) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => owner.OnStateChanged(deviceId, newState);

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceRemoved(string deviceId) => owner.OnStateChanged(deviceId, DeviceState.NotPresent);

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia) owner.OnDefaultChanged();
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
