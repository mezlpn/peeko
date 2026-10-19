namespace Peeko.Core.Media;

/// <summary>Nama dan warna chip aplikasi sumber (Music.dc.html: chip "● Spotify").</summary>
public sealed record MediaSourceApp(string Name, string? ColorHex)
{
    private static readonly (string Match, string Name, string? Color)[] Known =
    [
        ("spotify", "Spotify", "#1DB954"),
        ("chrome", "Chrome", "#4C8BF5"),
        ("msedge", "Edge", "#2FA0E8"),
        ("firefox", "Firefox", "#FF7139"),
        ("308046b0af4a39cb", "Firefox", "#FF7139"), // AUMID Firefox berupa hash
        ("opera", "Opera", "#FF1B2D"),
        ("brave", "Brave", "#FB542B"),
        ("zunemusic", "Media Player", "#E3008C"),
        ("zunevideo", "Film & TV", "#E3008C"),
        ("vlc", "VLC", "#FF8800"),
        ("foobar2000", "foobar2000", null),
        ("musicbee", "MusicBee", null),
        ("applemusic", "Apple Music", "#FA2D48"),
        ("itunes", "iTunes", "#FA2D48"),
        ("youtube", "YouTube Music", "#FF0033"),
    ];

    public static MediaSourceApp FromAppId(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return new("Pemutar", null);

        var lower = appId.ToLowerInvariant();
        foreach (var (match, name, color) in Known)
        {
            if (lower.Contains(match))
                return new(name, color);
        }

        // "Penerbit.Aplikasi_hash!App" → "Aplikasi"; "nama.exe" → "nama"
        var name2 = appId;
        int bang = name2.IndexOf('!');
        if (bang >= 0) name2 = name2[..bang];
        int underscore = name2.IndexOf('_');
        if (underscore > 0) name2 = name2[..underscore];
        if (name2.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name2 = name2[..^4];
        int dot = name2.LastIndexOf('.');
        if (dot >= 0 && dot < name2.Length - 1) name2 = name2[(dot + 1)..];

        return new(name2.Length > 0 ? char.ToUpperInvariant(name2[0]) + name2[1..] : "Pemutar", null);
    }
}
