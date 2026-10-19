using System.Diagnostics;
using System.Windows.Threading;
using Celah.App.Music;
using Celah.Core.Lyrics;
using Celah.Core.Media;
using Microsoft.Extensions.Logging;

namespace Celah.App.Lyrics;

/// <summary>
/// Mengambil lirik saat lagu berganti dan menyinkronkan baris aktif dengan posisi lagu.
/// Tidak ada polling: satu timer sekali-jalan dipasang tepat sebelum baris berikutnya, dan
/// hanya selama modul Lirik terlihat.
/// </summary>
public sealed class LyricsController : IDisposable
{
    // Baris dipicu sedikit lebih awal supaya terasa tepat waktu (Motion.dc.html).
    private static readonly TimeSpan Lead = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(6);

    private readonly MusicController _music;
    private readonly LyricsService _service;
    private readonly ILogger<LyricsController> _log;

    private readonly DispatcherTimer _trackDebounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer _loadingDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _lineTimer = new(DispatcherPriority.Render);

    private string? _trackKey;
    private CancellationTokenSource? _fetch;
    private SyncedLyrics? _lyrics;
    private int _activeIndex = int.MinValue;
    private bool _visible;

    public LyricsController(MusicController music, LyricsService service, ILogger<LyricsController> log)
    {
        _music = music;
        _service = service;
        _log = log;
        ViewModel = new LyricsViewModel(music.ViewModel, () => FetchAsync(retry: true), OpenInBrowser,
            () => OpenSettingsRequested?.Invoke());

        _trackDebounce.Tick += (_, _) =>
        {
            _trackDebounce.Stop();
            _ = FetchAsync(retry: false);
        };
        _loadingDelay.Tick += (_, _) =>
        {
            // "Memuat" baru ditampilkan jika pencarian lebih dari 250 ms (Lyrics.dc.html).
            _loadingDelay.Stop();
            if (_fetch is not null) ViewModel.State = LyricsViewState.Loading;
        };
        _lineTimer.Tick += (_, _) =>
        {
            _lineTimer.Stop();
            Resync(animate: true);
        };

        music.StateApplied += OnStateApplied;
    }

    public LyricsViewModel ViewModel { get; }

    /// <summary>Tombol "Buka pengaturan" di state lirik dimatikan.</summary>
    public event Action? OpenSettingsRequested;

    private bool _fetchEnabled = true;

    /// <summary>"Ambil lirik dari internet". Mati: tidak ada request sama sekali ke LRCLIB.</summary>
    public void SetFetchEnabled(bool enabled)
    {
        if (_fetchEnabled == enabled) return;
        _fetchEnabled = enabled;
        _trackKey = null; // paksa lagu sekarang diproses ulang
        if (_music.State is { } state) OnStateApplied(state);
        else OnStateApplied(null);
    }

    /// <summary>Lirik tersinkron baru (atau null) — modul membangun ulang daftar baris.</summary>
    public event Action<SyncedLyrics?>? LinesChanged;

    /// <summary>(indeks baris aktif, animasikan?) — −1 berarti sebelum baris pertama.</summary>
    public event Action<int, bool>? ActiveLineChanged;

    public void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (visible) Resync(animate: false);
        else _lineTimer.Stop();
    }

    /// <summary>Klik baris lirik: lompat ke timestamp-nya (jika sumber mendukung seek).</summary>
    public void SeekToLine(int index)
    {
        if (_lyrics is null || index < 0 || index >= _lyrics.Lines.Count) return;
        _ = _music.SeekToAsync(_lyrics.Lines[index].Time);
    }

    private void OnStateApplied(MediaState? state)
    {
        if (state is null)
        {
            CancelFetch();
            _trackDebounce.Stop();
            _trackKey = null;
            SetLyrics(null);
            ViewModel.State = LyricsViewState.None;
            return;
        }

        if (state.Track.Key != _trackKey)
        {
            _trackKey = state.Track.Key;
            CancelFetch();
            SetLyrics(null);
            if (!_fetchEnabled)
            {
                _trackDebounce.Stop();
                ViewModel.State = LyricsViewState.Disabled;
                return;
            }
            ViewModel.State = LyricsViewState.Loading;
            // Tunggu sebentar: durasi lagu kadang menyusul, dan skip lagu beruntun tidak perlu dicari semua.
            _trackDebounce.Stop();
            _trackDebounce.Start();
            return;
        }

        // Lagu sama: play/pause/seek mengubah jadwal baris berikutnya.
        Resync(animate: true);
    }

    private async Task FetchAsync(bool retry)
    {
        if (_music.State is not { } state) return;
        var key = state.Track.Key;
        var track = state.Track;
        var queries = TrackQueryBuilder.Build(track.Title, track.Artist, track.Album, state.Timeline?.Duration ?? TimeSpan.Zero);

        CancelFetch();
        var cts = new CancellationTokenSource(FetchTimeout);
        _fetch = cts;
        if (retry) ViewModel.State = LyricsViewState.Loading;
        _loadingDelay.Start();

        LyricsResult result;
        try
        {
            result = await _service.GetAsync(queries, retry, cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (_fetch != cts) return; // diganti lagu lain
            result = new LyricsResult.NotFound(); // batas waktu 6 dtk: anggap tidak ditemukan
        }
        finally
        {
            if (_fetch == cts) _fetch = null;
            cts.Dispose();
        }

        if (key != _trackKey) return;
        _loadingDelay.Stop();
        _log.LogDebug("Lirik {Title}: {Result}", track.Title, result.GetType().Name);

        switch (result)
        {
            case LyricsResult.Synced s:
                SetLyrics(s.Lyrics);
                ViewModel.State = LyricsViewState.Synced;
                break;
            case LyricsResult.Plain p:
                SetLyrics(null);
                ViewModel.PlainText = p.Text.Trim();
                ViewModel.State = LyricsViewState.Plain;
                break;
            case LyricsResult.Instrumental:
                SetLyrics(null);
                ViewModel.State = LyricsViewState.Instrumental;
                break;
            case LyricsResult.Failed f:
                _log.LogWarning("Gagal mengambil lirik: {Reason}", f.Reason);
                SetLyrics(null);
                ViewModel.State = LyricsViewState.Failed;
                break;
            default:
                SetLyrics(null);
                ViewModel.State = LyricsViewState.NotFound;
                break;
        }
    }

    private void CancelFetch()
    {
        _loadingDelay.Stop();
        var fetch = _fetch;
        _fetch = null;
        fetch?.Cancel();
    }

    private void SetLyrics(SyncedLyrics? lyrics)
    {
        _lyrics = lyrics;
        _activeIndex = int.MinValue;
        _lineTimer.Stop();
        LinesChanged?.Invoke(lyrics);
        Resync(animate: false);
    }

    private void Resync(bool animate)
    {
        _lineTimer.Stop();
        if (_lyrics is null || !_visible) return;

        var position = _music.Position + Lead;
        int index = _lyrics.IndexAt(position);
        if (index != _activeIndex)
        {
            bool jump = Math.Abs(index - _activeIndex) > 1; // seek jauh: tanpa animasi geser panjang
            _activeIndex = index;
            ActiveLineChanged?.Invoke(index, animate && !jump);
        }

        if (_music.State?.Playback.IsPlaying != true) return;
        if (_lyrics.NextTime(index) is not { } next) return;

        var wait = TimeSpan.FromTicks((long)((next - position).Ticks / _music.PlaybackRate));
        _lineTimer.Interval = wait < TimeSpan.FromMilliseconds(15) ? TimeSpan.FromMilliseconds(15) : wait;
        _lineTimer.Start();
    }

    private void OpenInBrowser()
    {
        if (_music.State is not { } state) return;
        var query = $"{state.Track.Artist} {state.Track.Title}".Trim();
        var url = $"{LyricsService.BaseUrl}/search/{Uri.EscapeDataString(query)}";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _log.LogWarning(ex, "Gagal membuka peramban");
        }
    }

    public void Dispose()
    {
        _music.StateApplied -= OnStateApplied;
        CancelFetch();
        _trackDebounce.Stop();
        _lineTimer.Stop();
    }
}
