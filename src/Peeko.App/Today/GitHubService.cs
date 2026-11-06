using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Peeko.App.Settings;
using Peeko.Core.Peeks;

namespace Peeko.App.Today;

public sealed record PullRequest(string Title, string Repo, int Number, string Url, string Author);

/// <summary>
/// Pull request terbuka yang meminta review darimu (pencarian GitHub "review-requested:username").
/// Mati selama username kosong. Dicek saat mulai lalu tiap 15 menit; repo publik cukup dengan
/// username, repo privat butuh token (fine-grained, izin baca pull request) yang diisi sendiri di
/// Pengaturan dan hanya disimpan di laptop ini.
/// </summary>
public sealed class GitHubService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly ILogger<GitHubService> _log;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(15) };
    private readonly HashSet<string> _seen = [];
    private string _key = "";
    private bool _first = true;

    public GitHubService(SettingsService settings, ILogger<GitHubService> log)
    {
        _settings = settings;
        _log = log;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Peeko");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public IReadOnlyList<PullRequest> Reviews { get; private set; } = [];

    public string? LastError { get; private set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_settings.Current.GitHubUser);

    public event Action? Updated;

    public event Action<PeekRequest>? PeekRequested;

    public void Apply()
    {
        var key = $"{_settings.Current.GitHubUser}|{_settings.Current.GitHubToken}";
        if (key == _key) return;
        _key = key;
        _seen.Clear();
        _first = true;
        if (!IsEnabled)
        {
            _timer.Stop();
            Reviews = [];
            LastError = null;
            Updated?.Invoke();
            return;
        }
        _timer.Start();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (!IsEnabled) return;
        var user = _settings.Current.GitHubUser!.Trim().TrimStart('@');
        var q = Uri.EscapeDataString($"is:pr is:open archived:false review-requested:{user}");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/search/issues?q={q}&sort=updated&per_page=20");
        if (_settings.Current.GitHubToken is { Length: > 0 } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        try
        {
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                LastError = (int)response.StatusCode switch
                {
                    401 => "Token GitHub ditolak",
                    403 or 429 => "GitHub membatasi permintaan, dicoba lagi nanti",
                    422 => "Username GitHub tidak ditemukan",
                    _ => $"GitHub menolak ({(int)response.StatusCode})",
                };
                Updated?.Invoke();
                return;
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var list = new List<PullRequest>();
            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                var url = item.GetProperty("html_url").GetString() ?? "";
                // https://github.com/pemilik/repo/pull/12
                var parts = new Uri(url).AbsolutePath.Trim('/').Split('/');
                var repo = parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : "";
                list.Add(new PullRequest(item.GetProperty("title").GetString() ?? "", repo, item.GetProperty("number").GetInt32(), url,
                    item.GetProperty("user").GetProperty("login").GetString() ?? ""));
            }
            LastError = null;
            Reviews = list;
            foreach (var pr in list.Where(p => _seen.Add(p.Url)))
            {
                // Saat pertama dicek, yang sudah ada tidak dikabarkan lagi.
                if (_first) continue;
                PeekRequested?.Invoke(new PeekRequest(PeekKind.PullRequest) { Text = pr.Title, Detail = $"{pr.Repo} #{pr.Number}" });
            }
            _first = false;
            Updated?.Invoke();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or UriFormatException)
        {
            _log.LogInformation("GitHub gagal dicek: {Message}", ex.Message);
            LastError = "GitHub belum bisa dicek";
            Updated?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _http.Dispose();
    }
}
