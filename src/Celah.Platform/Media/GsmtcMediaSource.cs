using System.Runtime.InteropServices;
using Celah.Core.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Celah.Platform.Media;

/// <summary>
/// Membaca sesi media Windows (Spotify, browser, Media Player, dll.) lewat
/// GlobalSystemMediaTransportControlsSessionManager. Event dikirim dari thread latar.
/// </summary>
public sealed class GsmtcMediaSource : IMediaSource
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(60);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly List<Session> _watched = [];
    private SessionManager? _manager;
    private Session? _session;
    private int _refreshQueued;
    private bool _disposed;

    private string? _thumbnailKey;
    private byte[]? _thumbnail;

    public MediaState? Current { get; private set; }

    public event Action<MediaState?>? Changed;

    public async Task StartAsync()
    {
        _manager = await SessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => { WatchSessions(); QueueRefresh(); };
        _manager.CurrentSessionChanged += (_, _) => QueueRefresh();
        WatchSessions();
        await RefreshAsync();
    }

    /// <summary>Pantau semua sesi supaya bisa pindah ke sesi yang sedang diputar.</summary>
    private void WatchSessions()
    {
        if (_manager is null) return;
        lock (_watched)
        {
            foreach (var s in _watched)
            {
                s.MediaPropertiesChanged -= OnSessionChanged;
                s.PlaybackInfoChanged -= OnSessionChanged;
                s.TimelinePropertiesChanged -= OnSessionChanged;
            }
            _watched.Clear();

            foreach (var s in _manager.GetSessions())
            {
                s.MediaPropertiesChanged += OnSessionChanged;
                s.PlaybackInfoChanged += OnSessionChanged;
                s.TimelinePropertiesChanged += OnSessionChanged;
                _watched.Add(s);
            }
        }
    }

    private void OnSessionChanged(Session sender, object args) => QueueRefresh();

    /// <summary>Beberapa pemutar mengirim beberapa event beruntun; gabungkan jadi satu refresh.</summary>
    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
        _ = Task.Delay(Debounce).ContinueWith(_ => RefreshAsync(), TaskScheduler.Default).Unwrap();
    }

    private Session? PickSession()
    {
        if (_manager is null) return null;
        var current = _manager.GetCurrentSession();
        if (current is not null && IsPlaying(current)) return current;

        // Utamakan sesi yang sedang diputar (mis. YouTube diputar saat Spotify dijeda).
        var sessions = _manager.GetSessions();
        return sessions.FirstOrDefault(IsPlaying) ?? current ?? sessions.FirstOrDefault();

        static bool IsPlaying(Session s)
        {
            try
            {
                return s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch (COMException)
            {
                return false; // aplikasi baru saja ditutup
            }
        }
    }

    private async Task RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            if (_disposed) return;

            var session = PickSession();
            _session = session;
            var state = session is null ? null : await ReadStateAsync(session);

            Current = state;
            Changed?.Invoke(state);
        }
        catch (COMException)
        {
            // Sesi hilang di tengah pembacaan; event berikutnya akan memperbaiki state.
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<MediaState?> ReadStateAsync(Session session)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        if (props is null) return null;

        var info = session.GetPlaybackInfo();
        var timelineProps = session.GetTimelineProperties();
        var controls = info.Controls;

        var title = props.Title ?? "";
        var artist = string.IsNullOrEmpty(props.Artist) ? props.AlbumArtist ?? "" : props.Artist;
        var key = $"{session.SourceAppUserModelId}\u001F{title}\u001F{artist}\u001F{props.AlbumTitle}";

        if (key != _thumbnailKey)
        {
            _thumbnailKey = key;
            _thumbnail = null;
        }
        // Spotify kadang mengirim judul dulu, cover menyusul di event berikutnya.
        if (_thumbnail is null && props.Thumbnail is not null)
            _thumbnail = await ReadThumbnailAsync(props.Thumbnail);

        var duration = timelineProps.EndTime - timelineProps.StartTime;
        MediaTimeline? timeline = duration > TimeSpan.Zero
            ? new MediaTimeline(timelineProps.Position - timelineProps.StartTime, duration, timelineProps.LastUpdatedTime)
            : null;

        var playback = new MediaPlayback(
            IsPlaying: info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            CanPlayPause: controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled,
            CanNext: controls.IsNextEnabled,
            CanPrevious: controls.IsPreviousEnabled,
            CanSeek: controls.IsPlaybackPositionEnabled && timeline is not null,
            Rate: info.PlaybackRate ?? 1);

        var track = new MediaTrack(title, artist, props.AlbumTitle ?? "", session.SourceAppUserModelId, _thumbnail);
        return new MediaState(track, playback, timeline);
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 8_000_000) return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            uint size = (uint)stream.Size;
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public Task TogglePlayPauseAsync() => Run(s => s.TryTogglePlayPauseAsync().AsTask());

    public Task NextAsync() => Run(s => s.TrySkipNextAsync().AsTask());

    public Task PreviousAsync() => Run(s => s.TrySkipPreviousAsync().AsTask());

    public Task SeekAsync(TimeSpan position) => Run(s =>
    {
        var start = s.GetTimelineProperties().StartTime;
        return s.TryChangePlaybackPositionAsync((start + position).Ticks).AsTask();
    });

    private async Task Run(Func<Session, Task<bool>> command)
    {
        var session = _session;
        if (session is null) return;
        try
        {
            await command(session);
        }
        catch (COMException)
        {
            QueueRefresh();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_watched)
        {
            foreach (var s in _watched)
            {
                s.MediaPropertiesChanged -= OnSessionChanged;
                s.PlaybackInfoChanged -= OnSessionChanged;
                s.TimelinePropertiesChanged -= OnSessionChanged;
            }
            _watched.Clear();
        }
    }
}
