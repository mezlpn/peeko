namespace Celah.Core.Media;

/// <summary>Potret lengkap sesi media yang sedang aktif (dari Windows media session/GSMTC).</summary>
public sealed record MediaState(MediaTrack Track, MediaPlayback Playback, MediaTimeline? Timeline);

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

/// <summary>Sumber media (diimplementasikan di Celah.Platform). Event bisa datang dari thread mana saja.</summary>
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
}
