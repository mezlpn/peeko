using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Peeko.Core.Media;
using Peeko.Core.Notch;
using Peeko.Platform.Audio;
using Microsoft.Extensions.Logging;

namespace Peeko.App.Music;

/// <summary>
/// Menerjemahkan sesi media Windows ke <see cref="MusicViewModel"/>. Timer hanya hidup saat
/// hasilnya terlihat: progress saat panel terbuka, visualizer saat pil musik tampil.
/// </summary>
public sealed class MusicController : IDisposable
{
    // Desain: 30 dtk. Revisi #10: Kedip harus cepat kembali setelah musik dijeda. Tidak 0 karena
    // saat ganti lagu beberapa pemutar sempat melaporkan "dijeda" sesaat.
    private static readonly TimeSpan PausedLinger = TimeSpan.FromSeconds(1.5);
    private const int BarCount = SpectrumAnalyzer.BandCount;

    private readonly IMediaSource _source;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<MusicController> _log;
    private readonly PlaybackClock _clock = new();
    private readonly AudioSpectrum _spectrum = new();

    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _visualizerTimer;
    private readonly DispatcherTimer _pausedTimer;


    private MediaState? _state;
    private string? _coverKey;
    private NotchState _notchState = NotchState.Collapsed;
    private bool _musicTabVisible;
    private bool _pausedLongEnough;
    private CollapsedActivity _activity = CollapsedActivity.Idle;

    public MusicController(IMediaSource source, Dispatcher dispatcher, ILogger<MusicController> log)
    {
        _source = source;
        _dispatcher = dispatcher;
        _log = log;

        ViewModel = new MusicViewModel(source.TogglePlayPauseAsync, source.NextAsync, source.PreviousAsync, source.SwitchSessionAsync);

        _progressTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        // 30× per detik; tiap bar mengejar levelnya dengan pegas, jadi gerakannya tetap halus.
        _visualizerTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _visualizerTimer.Tick += (_, _) => UpdateVisualizer();

        _pausedTimer = new DispatcherTimer { Interval = PausedLinger };
        _pausedTimer.Tick += (_, _) =>
        {
            _pausedTimer.Stop();
            _pausedLongEnough = true;
            UpdateActivity();
        };
    }

    public MusicViewModel ViewModel { get; }

    /// <summary>Isi pil yang diinginkan modul musik (Music saat diputar atau baru saja dijeda).</summary>
    public event Action<CollapsedActivity>? ActivityChanged;

    /// <summary>Level 5 pita frekuensi (0–1) dan apakah lagu sedang diputar, untuk visualizer.</summary>
    public event Action<double[], bool>? LevelsUpdated;

    public CollapsedActivity Activity => _activity;

    /// <summary>State media terbaru sudah diterapkan (thread UI). Dipakai modul Lirik.</summary>
    public event Action<MediaState?>? StateApplied;

    public MediaState? State => _state;

    /// <summary>Posisi lagu sekarang hasil ekstrapolasi <see cref="PlaybackClock"/>.</summary>
    public TimeSpan Position => _clock.GetPosition(DateTimeOffset.Now);

    public double PlaybackRate => _state?.Playback.Rate is > 0 and var r ? r : 1;

    public Task SeekToAsync(TimeSpan position) =>
        _state?.Playback.CanSeek == true ? _source.SeekAsync(position) : Task.CompletedTask;

    public async Task StartAsync()
    {
        _source.Changed += state => _dispatcher.BeginInvoke(() => Apply(state));
        try
        {
            await _source.StartAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal membaca sesi media Windows");
        }
    }

    public Task SeekAsync(double fraction)
    {
        if (_state?.Timeline is not { } t || !_state.Playback.CanSeek) return Task.CompletedTask;
        var target = TimeSpan.FromTicks((long)(t.Duration.Ticks * Math.Clamp(fraction, 0, 1)));
        return _source.SeekAsync(target);
    }

    /// <summary>Geser ±detik dari posisi sekarang (scroll di atas slider).</summary>
    public Task SeekByAsync(TimeSpan delta)
    {
        if (_state?.Timeline is null || !_state.Playback.CanSeek) return Task.CompletedTask;
        var target = _clock.GetPosition(DateTimeOffset.Now) + delta;
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (target > _clock.Duration) target = _clock.Duration;
        return _source.SeekAsync(target);
    }

    /// <summary>Tab Musik sedang terlihat di panel yang terbuka.</summary>
    public void SetMusicTabVisible(bool visible)
    {
        _musicTabVisible = visible;
        UpdateTimers();
    }

    public void SetNotchState(NotchState state)
    {
        _notchState = state;
        UpdateTimers();
        if (state == NotchState.Expanded) UpdateProgress();
    }

    private void Apply(MediaState? state)
    {
        var previous = _state;
        _state = state;
        var vm = ViewModel;

        if (state is null)
        {
            _clock.Reset();
            vm.HasSession = false;
            vm.SessionCount = 0;
            vm.IsPlaying = false;
            vm.CanPlayPause = vm.CanNext = vm.CanPrevious = vm.CanSeek = vm.HasTimeline = false;
            vm.Cover = null;
            _coverKey = null;
            UpdateActivity();
            UpdateTimers();
            StateApplied?.Invoke(null);
            return;
        }

        var track = state.Track;
        var playback = state.Playback;

        if (previous?.Track.Key != track.Key)
            _log.LogDebug("Lagu: {Title} — {Artist} ({App})", track.Title, track.Artist, track.SourceAppId);

        _clock.Update(state.Timeline, playback.IsPlaying, playback.Rate, DateTimeOffset.Now);

        var app = MediaSourceApp.FromAppId(track.SourceAppId);
        vm.HasSession = true;
        vm.SessionCount = state.SessionCount;
        vm.SessionIndex = state.SessionIndex;
        vm.Title = string.IsNullOrWhiteSpace(track.Title) ? "Tanpa judul" : track.Title;
        vm.Artist = track.Artist;
        vm.SourceName = app.Name;
        vm.SourceBrush = app.ColorHex is { } hex ? Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))) : (Brush)App.Current.Resources["Peeko.Brush.TextTertiary"];
        vm.IsPlaying = playback.IsPlaying;
        vm.CanPlayPause = playback.CanPlayPause;
        vm.CanNext = playback.CanNext;
        vm.CanPrevious = playback.CanPrevious;
        vm.CanSeek = playback.CanSeek;
        vm.HasTimeline = state.Timeline is not null;

        // Cover bisa datang belakangan untuk lagu yang sama, jadi kuncinya ikut ada/tidaknya gambar.
        var coverKey = track.Thumbnail is null ? track.Key + "\u001Fnone" : track.Key;
        if (coverKey != _coverKey)
        {
            _coverKey = coverKey;
            _ = LoadCoverAsync(track.Thumbnail, coverKey);
        }

        if (playback.IsPlaying)
        {
            _pausedTimer.Stop();
            _pausedLongEnough = false;
        }
        else if (previous?.Playback.IsPlaying != false && !_pausedLongEnough)
        {
            _pausedTimer.Stop();
            _pausedTimer.Start();
        }

        UpdateProgress();
        UpdateActivity();
        UpdateTimers();
        StateApplied?.Invoke(state);
    }

    private async Task LoadCoverAsync(byte[]? bytes, string key)
    {
        if (bytes is null)
        {
            ViewModel.Cover = null;
            ViewModel.AccentBrush = (Brush)App.Current.Resources["Peeko.Brush.Accent"];
            return;
        }

        var (image, accent) = await Task.Run(() => DecodeCover(bytes));
        if (key != _coverKey) return; // lagu sudah berganti lagi

        ViewModel.Cover = image;
        ViewModel.AccentBrush = accent;
    }

    private static (ImageSource? Image, Brush Accent) DecodeCover(byte[] bytes)
    {
        try
        {
            // 176 px = cover 88 px pada skala 200%; cukup tajam tanpa memakan memori.
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 176;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();

            var small = new FormatConvertedBitmap(new TransformedBitmap(image,
                new ScaleTransform(48.0 / image.PixelWidth, 48.0 / image.PixelHeight)), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[small.PixelWidth * small.PixelHeight * 4];
            small.CopyPixels(pixels, small.PixelWidth * 4, 0);
            var c = CoverPalette.DominantColor(pixels);

            return (image, Freeze(new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B))));
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException)
        {
            var f = CoverPalette.Fallback;
            return (null, Freeze(new SolidColorBrush(Color.FromRgb(f.R, f.G, f.B))));
        }
    }

    private void UpdateProgress()
    {
        if (_state?.Timeline is null) return;
        var now = DateTimeOffset.Now;
        var position = _clock.GetPosition(now);
        var duration = _clock.Duration;
        ViewModel.Progress = duration > TimeSpan.Zero ? position / duration : 0;
        ViewModel.PositionText = MusicViewModel.FormatTime(position);
        ViewModel.DurationText = MusicViewModel.FormatTime(duration);
    }

    private void UpdateActivity()
    {
        bool live = _state is not null && (_state.Playback.IsPlaying || !_pausedLongEnough);
        var activity = live ? CollapsedActivity.Music : CollapsedActivity.Idle;
        if (activity == _activity) return;
        _activity = activity;
        ActivityChanged?.Invoke(activity);
        UpdateTimers();
    }

    private void UpdateTimers()
    {
        bool playing = _state?.Playback.IsPlaying == true;

        SetRunning(_progressTimer, playing && _notchState == NotchState.Expanded && _state?.Timeline is not null);
        bool pillShowsBars = _notchState == NotchState.Collapsed
            && _activity is CollapsedActivity.Music or CollapsedActivity.MusicAndFocus;
        bool visualize = playing && (pillShowsBars || (_notchState == NotchState.Expanded && _musicTabVisible));
        SetRunning(_visualizerTimer, visualize);
        // Tangkapan audio hanya hidup selama bar terlihat dan lagu berjalan.
        if (visualize) _spectrum.Start(); else _spectrum.Stop();

        if (!playing) LevelsUpdated?.Invoke(new double[BarCount], false); // bar turun jadi titik
    }

    private static void SetRunning(DispatcherTimer timer, bool run)
    {
        if (run && !timer.IsEnabled) timer.Start();
        else if (!run && timer.IsEnabled) timer.Stop();
    }

    private void UpdateVisualizer() =>
        LevelsUpdated?.Invoke(Array.ConvertAll(_spectrum.Levels, l => (double)l), true);

    private static T Freeze<T>(T freezable) where T : System.Windows.Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    public void Dispose()
    {
        _progressTimer.Stop();
        _visualizerTimer.Stop();
        _pausedTimer.Stop();
        _source.Dispose();
        _spectrum.Dispose();
    }
}
