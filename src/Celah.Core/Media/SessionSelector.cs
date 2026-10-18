namespace Celah.Core.Media;

/// <summary>
/// Memilih sesi media yang ditampilkan saat ada beberapa pemutar (mis. YouTube di Chrome dan
/// YouTube Music di Brave). Aturan (revisi #13):
/// <list type="number">
/// <item>Sesi terpilih dipertahankan walau dijeda; tidak ikut "sesi sekarang" versi Windows.</item>
/// <item>Sesi lain yang <i>baru mulai</i> diputar mengambil alih, kecuali pengguna memilih sesi
/// secara manual dan sesi itu masih diputar.</item>
/// <item>Jika sesi terpilih hilang: yang sedang diputar → sesi sekarang versi Windows → yang pertama.</item>
/// </list>
/// </summary>
public sealed class SessionSelector<T> where T : class
{
    private readonly Dictionary<T, bool> _wasPlaying = new();
    private bool _manual;

    public T? Selected { get; private set; }

    public T? Update(IReadOnlyList<(T Session, bool Playing)> sessions, T? systemCurrent)
    {
        var newlyPlaying = sessions
            .Where(s => s.Playing && !_wasPlaying.GetValueOrDefault(s.Session))
            .Select(s => s.Session)
            .ToList();

        bool selectedPlaying = false;
        bool selectedPresent = false;
        foreach (var (session, playing) in sessions)
        {
            if (Equals(session, Selected))
            {
                selectedPresent = true;
                selectedPlaying = playing;
            }
        }

        _wasPlaying.Clear();
        foreach (var (session, playing) in sessions)
            _wasPlaying[session] = playing;

        if (!selectedPresent)
        {
            Selected = null;
            _manual = false;
        }

        var takeover = newlyPlaying.FirstOrDefault(s => !Equals(s, Selected));
        if (takeover is not null && !(_manual && selectedPlaying))
        {
            Selected = takeover;
            _manual = false;
        }

        if (Selected is null)
        {
            Selected = sessions.FirstOrDefault(s => s.Playing).Session
                ?? sessions.Select(s => s.Session).FirstOrDefault(s => Equals(s, systemCurrent))
                ?? sessions.FirstOrDefault().Session;
        }

        return Selected;
    }

    /// <summary>Pindah ke sesi sebelumnya/berikutnya (panah di modul Musik).</summary>
    public T? Cycle(IReadOnlyList<T> sessions, int direction)
    {
        if (sessions.Count == 0) return Selected = null;

        int index = -1;
        for (int i = 0; i < sessions.Count; i++)
        {
            if (Equals(sessions[i], Selected)) index = i;
        }

        int next = index < 0 ? 0 : ((index + direction) % sessions.Count + sessions.Count) % sessions.Count;
        Selected = sessions[next];
        _manual = true;
        return Selected;
    }

    public int IndexIn(IReadOnlyList<T> sessions)
    {
        for (int i = 0; i < sessions.Count; i++)
        {
            if (Equals(sessions[i], Selected)) return i;
        }
        return -1;
    }
}
