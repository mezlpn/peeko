using System.Runtime.InteropServices;
using Peeko.Core.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Peeko.Platform.Media;

/// <summary>
/// Membaca sesi media Windows (Spotify, browser, Media Player, dll.) lewat
/// GlobalSystemMediaTransportControlsSessionManager. Event dikirim dari thread latar.
/// Sesi yang ditampilkan dipilih oleh <see cref="SessionSelector{T}"/>.
/// </summary>
public sealed class GsmtcMediaSource : IMediaSource
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(60);
    private const int ThumbnailCacheSize = 8;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly List<Session> _watched = [];
    private readonly SessionSelector<string> _selector = new();
    private readonly Dictionary<string, byte[]> _thumbnails = [];
    private SessionManager? _manager;
    private Session? _session;
    private int _refreshQueued;
    private bool _disposed;

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

    /// <summary>Pantau semua sesi supaya tahu kapan sesi lain mulai diputar.</summary>
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

    /// <summary>
    /// Semua sesi beserta kunci yang stabil antar-refresh: AppUserModelId, ditambah nomor urut
    /// jika satu aplikasi punya beberapa sesi.
    /// </summary>
    private List<(string Key, Session Session)> ListSessions()
    {
        var result = new List<(string, Session)>();
        if (_manager is null) return result;

        var seen = new Dictionary<string, int>();
        foreach (var s in _manager.GetSessions())
        {
            var id = s.SourceAppUserModelId ?? "";
            int n = seen.GetValueOrDefault(id);
            seen[id] = n + 1;
            result.Add((n == 0 ? id : $"{id}#{n}", s));
        }
        return result;
    }

    private static bool IsPlaying(Session s)
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

    private async Task RefreshAsync(bool keepSelection = false)
    {
        await _refreshLock.WaitAsync();
        try
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            if (_disposed || _manager is null) return;

            var sessions = ListSessions();
            string? selected;
            if (keepSelection)
            {
                selected = _selector.Selected;
            }
            else
            {
                var currentId = _manager.GetCurrentSession()?.SourceAppUserModelId;
                selected = _selector.Update(
                    sessions.Select(s => (s.Key, IsPlaying(s.Session))).ToList(),
                    currentId);
            }

            int index = sessions.FindIndex(s => s.Key == selected);
            _session = index >= 0 ? sessions[index].Session : null;

            MediaState? state = null;
            if (_session is not null && await ReadStateAsync(_session) is { } read)
                state = read with { SessionIndex = index, SessionCount = sessions.Count };

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
        var track = new MediaTrack(title, artist, props.AlbumTitle ?? "", session.SourceAppUserModelId, null);

        // Cover di-cache per lagu supaya pindah sumber bolak-balik tidak memuat ulang gambar.
        // Spotify kadang mengirim judul dulu, cover menyusul di event berikutnya.
        if (!_thumbnails.TryGetValue(track.Key, out var thumbnail) && props.Thumbnail is not null)
        {
            thumbnail = await ReadThumbnailAsync(props.Thumbnail);
            if (thumbnail is not null)
            {
                if (_thumbnails.Count >= ThumbnailCacheSize) _thumbnails.Clear();
                _thumbnails[track.Key] = thumbnail;
            }
        }

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

        return new MediaState(track with { Thumbnail = thumbnail }, playback, timeline);
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

    public async Task SwitchSessionAsync(int direction)
    {
        await _refreshLock.WaitAsync();
        try
        {
            _selector.Cycle(ListSessions().Select(s => s.Key).ToList(), direction);
        }
        finally
        {
            _refreshLock.Release();
        }
        await RefreshAsync(keepSelection: true);
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
