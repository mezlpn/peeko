namespace Celah.Core.Media;

/// <summary>
/// Menghitung posisi lagu secara lokal. Windows hanya mengirim posisi saat ada perubahan
/// (play, pause, seek, ganti lagu), jadi di antara laporan posisi diekstrapolasi:
/// <c>posisi = posisi_laporan + (sekarang − waktu_laporan) × rate</c> selama lagu diputar.
/// </summary>
public sealed class PlaybackClock
{
    // Laporan dengan waktu di luar rentang ini dianggap rusak (beberapa pemutar mengirim
    // LastUpdatedTime default/1601 atau jam yang melenceng).
    private static readonly TimeSpan MaxReportAge = TimeSpan.FromHours(12);
    private static readonly TimeSpan MaxReportFuture = TimeSpan.FromSeconds(2);

    private TimeSpan _anchorPosition;
    private DateTimeOffset _anchorTime;
    private DateTimeOffset? _lastReport;
    private TimeSpan _lastReportedPosition;
    private bool _playing;
    private double _rate = 1;

    public TimeSpan Duration { get; private set; }

    public bool IsPlaying => _playing;

    public void Reset()
    {
        _anchorPosition = TimeSpan.Zero;
        _anchorTime = default;
        _lastReport = null;
        _lastReportedPosition = TimeSpan.Zero;
        _playing = false;
        _rate = 1;
        Duration = TimeSpan.Zero;
    }

    public void Update(MediaTimeline? timeline, bool playing, double rate, DateTimeOffset now)
    {
        rate = rate > 0 ? rate : 1;

        bool isNewReport = timeline is not null
            && (timeline.LastUpdated != _lastReport || timeline.Position != _lastReportedPosition);

        if (timeline is not null)
            Duration = timeline.Duration;

        if (isNewReport)
        {
            _lastReport = timeline!.LastUpdated;
            _lastReportedPosition = timeline.Position;

            var reportedAt = timeline.LastUpdated;
            bool sane = reportedAt <= now + MaxReportFuture && reportedAt >= now - MaxReportAge;
            if (!sane) reportedAt = now;

            _anchorPosition = timeline.Position;
            _anchorTime = reportedAt;
            _playing = playing;
            _rate = rate;
            return;
        }

        // Tidak ada laporan posisi baru: hanya status putar/jeda atau rate yang berubah.
        // Bekukan/lanjutkan dari posisi hasil hitungan saat ini, bukan dari laporan lama.
        if (playing != _playing || rate != _rate)
        {
            _anchorPosition = GetPosition(now);
            _anchorTime = now;
            _playing = playing;
            _rate = rate;
        }
    }

    public TimeSpan GetPosition(DateTimeOffset now)
    {
        var position = _anchorPosition;
        if (_playing && _anchorTime != default)
            position += TimeSpan.FromTicks((long)((now - _anchorTime).Ticks * _rate));

        if (position < TimeSpan.Zero) return TimeSpan.Zero;
        if (Duration > TimeSpan.Zero && position > Duration) return Duration;
        return position;
    }
}
