using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Peeko.App.Settings;
using Peeko.Core.Media;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Peeko.App.Music;

/// <summary>
/// Radio internet dari Radio Browser (radio-browser.info, gratis, tanpa akun). Diputar lewat
/// MediaPlayer Windows dengan System Media Transport Controls aktif, jadi radio juga tampil di tab
/// Musik, di kontrol media Windows, dan bisa dijeda dengan tombol media keyboard.
/// </summary>
public sealed class RadioService : IDisposable
{
    private static readonly string[] Mirrors = ["de1.api.radio-browser.info", "fi1.api.radio-browser.info", "de2.api.radio-browser.info"];

    private readonly SettingsService _settings;
    private readonly ILogger<RadioService> _log;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private MediaPlayer? _player;

    public RadioService(SettingsService settings, ILogger<RadioService> log)
    {
        _settings = settings;
        _log = log;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Peeko");
    }

    public RadioStation? Playing { get; private set; }

    public bool IsBuffering { get; private set; }

    public event Action? StateChanged;

    public bool IsEnabled => _settings.Current.RadioEnabled;

    /// <summary>Stasiun populer di negara itu, atau hasil pencarian nama.</summary>
    public async Task<IReadOnlyList<RadioStation>> SearchAsync(string? query, string country = "ID")
    {
        string path = string.IsNullOrWhiteSpace(query)
            ? $"/json/stations/search?countrycode={Uri.EscapeDataString(country)}&order=clickcount&reverse=true&limit=40&hidebroken=true"
            : $"/json/stations/search?name={Uri.EscapeDataString(query.Trim())}&order=clickcount&reverse=true&limit=40&hidebroken=true";
        foreach (var host in Mirrors)
        {
            try
            {
                var json = await _http.GetStringAsync($"https://{host}{path}");
                var list = JsonSerializer.Deserialize<List<RadioStation>>(json) ?? [];
                // Daftar M3U8/HLS kadang tidak bisa diputar MediaPlayer tanpa codec tambahan: taruh di belakang.
                return list.Where(s => !string.IsNullOrWhiteSpace(s.Url))
                    .DistinctBy(s => s.Name.Trim().ToLowerInvariant())
                    .OrderBy(s => s.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _log.LogInformation("Radio Browser {Host} gagal: {Message}", host, ex.Message);
            }
        }
        return [];
    }

    public IReadOnlyList<RadioStation> Favorites => _settings.Current.RadioFavorites;

    public bool IsFavorite(RadioStation s) => Favorites.Any(f => f.Id == s.Id);

    public void ToggleFavorite(RadioStation s) => _settings.Update(st =>
    {
        if (st.RadioFavorites.RemoveAll(f => f.Id == s.Id) == 0) st.RadioFavorites.Add(s);
    });

    public void Play(RadioStation station)
    {
        Stop(notify: false);
        _player = new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Media };
        _player.CommandManager.IsEnabled = true;
        var smtc = _player.SystemMediaTransportControls;
        var display = smtc.DisplayUpdater;
        display.Type = MediaPlaybackType.Music;
        display.MusicProperties.Title = station.Name;
        display.MusicProperties.Artist = "Radio · Peeko";
        display.Update();
        _player.PlaybackSession.PlaybackStateChanged += (s, _) =>
        {
            IsBuffering = s.PlaybackState is MediaPlaybackState.Opening or MediaPlaybackState.Buffering;
            Notify();
        };
        _player.MediaFailed += (_, e) =>
        {
            _log.LogInformation("Radio {Name} gagal diputar: {Error}", station.Name, e.ErrorMessage);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => Stop());
        };
        _player.Source = MediaSource.CreateFromUri(new Uri(station.Url));
        Playing = station;
        IsBuffering = true;
        _player.Play();
        // Hitungan klik membantu Radio Browser mengurutkan stasiun populer.
        _ = _http.GetAsync($"https://{Mirrors[0]}/json/url/{station.Id}").ContinueWith(_ => { }, TaskScheduler.Default);
        Notify();
    }

    public void Stop(bool notify = true)
    {
        if (_player is not null)
        {
            _player.Pause();
            _player.Source = null;
            _player.Dispose();
            _player = null;
        }
        Playing = null;
        IsBuffering = false;
        if (notify) Notify();
    }

    private void Notify() => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => StateChanged?.Invoke());

    public void Dispose()
    {
        Stop(notify: false);
        _http.Dispose();
    }
}
