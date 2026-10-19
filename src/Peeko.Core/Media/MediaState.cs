namespace Peeko.Core.Media;

/// <summary>Potret lengkap sesi media yang sedang ditampilkan (dari Windows media session/GSMTC).</summary>
/// <param name="SessionIndex">Urutan sesi ini di antara semua sesi (0-based).</param>
/// <param name="SessionCount">Jumlah sesi media yang terbuka; &gt; 1 memunculkan panah pemilih sumber.</param>
public sealed record MediaState(
    MediaTrack Track,
    MediaPlayback Playback,
    MediaTimeline? Timeline,
    int SessionIndex = 0,
    int SessionCount = 1);

/// <param name="SourceAppId">AppUserModelId pemutar, mis. "Spotify.exe" atau "Chrome".</param>
/// <param name="Thumbnail">Gambar cover mentah (PNG/JPEG), atau null jika tidak ada.</param>
public sealed record MediaTrack(
    string Title,
    string Artist,
    string Album,
    string SourceAppId,
    byte[]? Thumbnail)
{
    /// <summary>Identitas lagu untuk mendeteksi pergantian lagu (cover, lirik, warna).</summary>
    public string Key => $"{SourceAppId}\u001F{Title}\u001F{Artist}\u001F{Album}";
}

public sealed record MediaPlayback(
    bool IsPlaying,
    bool CanPlayPause,
    bool CanNext,
    bool CanPrevious,
    bool CanSeek,
    double Rate);

/// <param name="LastUpdated">Kapan pemutar terakhir melaporkan posisi (bukan waktu kita menerimanya).</param>
public sealed record MediaTimeline(
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset LastUpdated);

/// <summary>Sumber media (diimplementasikan di Peeko.Platform). Event bisa datang dari thread mana saja.</summary>
public interface IMediaSource : IDisposable
{
    /// <summary>State baru, atau null jika tidak ada sesi media sama sekali.</summary>
    event Action<MediaState?>? Changed;

    MediaState? Current { get; }

    Task StartAsync();

    Task TogglePlayPauseAsync();

    Task NextAsync();

    Task PreviousAsync();

    Task SeekAsync(TimeSpan position);

    /// <summary>Tampilkan sesi sebelumnya (−1) atau berikutnya (+1).</summary>
    Task SwitchSessionAsync(int direction);
}
