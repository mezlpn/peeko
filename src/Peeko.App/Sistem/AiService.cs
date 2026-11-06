using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Peeko.App.Settings;
using Peeko.Core.Productivity;

namespace Peeko.App.Sistem;

public sealed record AiMessage(bool FromUser, string Text);

/// <summary>
/// Asisten AI di tab AI. Penyedia: Claude (Anthropic), Gemini (Google), atau API kompatibel OpenAI
/// (OpenAI, OpenRouter, Groq, LM Studio, Ollama lokal di http://localhost:11434/v1). Kunci API diisi
/// sendiri di Pengaturan dan hanya dikirim ke penyedia itu. Mati selama belum diatur.
/// </summary>
public sealed class AiService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public AiService(SettingsService settings)
    {
        _settings = settings;
    }

    public bool IsConfigured => _settings.Current.AiProvider switch
    {
        "openai" => !string.IsNullOrWhiteSpace(_settings.Current.AiBaseUrl) && !string.IsNullOrWhiteSpace(_settings.Current.AiModel),
        "anthropic" or "gemini" => !string.IsNullOrWhiteSpace(_settings.Current.AiKey),
        _ => false,
    };

    public static string DefaultModel(string provider) => provider switch
    {
        "anthropic" => "claude-haiku-5-5",
        "gemini" => "gemini-2.5-flash",
        _ => "",
    };

    /// <summary>Kirim percakapan (pesan terakhir = pertanyaan pengguna). Terjemah: tanpa riwayat dan tanpa aksi.</summary>
    public async Task<string> AskAsync(IReadOnlyList<AiMessage> history, bool translate, CancellationToken ct)
    {
        var s = _settings.Current;
        var system = translate ? AiActions.TranslatePrompt : AiActions.SystemPrompt + $"\nSekarang {DateTime.Now:dddd, d MMMM yyyy HH:mm}.";
        var messages = translate ? history.TakeLast(1).ToList() : history.TakeLast(12).ToList();
        string model = string.IsNullOrWhiteSpace(s.AiModel) ? DefaultModel(s.AiProvider) : s.AiModel!.Trim();
        return s.AiProvider switch
        {
            "anthropic" => await AnthropicAsync(system, messages, model, s.AiKey!, ct),
            "gemini" => await GeminiAsync(system, messages, model, s.AiKey!, ct),
            _ => await OpenAiAsync(system, messages, model, s.AiBaseUrl!, s.AiKey, ct),
        };
    }

    private async Task<string> AnthropicAsync(string system, List<AiMessage> messages, string model, string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", key.Trim());
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = Json(new
        {
            model,
            max_tokens = 600,
            system,
            messages = messages.Select(m => new { role = m.FromUser ? "user" : "assistant", content = m.Text }),
        });
        var json = await SendAsync(req, ct);
        return string.Concat(json["content"]!.AsArray().Where(c => (string?)c?["type"] == "text").Select(c => (string?)c!["text"]));
    }

    private async Task<string> GeminiAsync(string system, List<AiMessage> messages, string model, string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        req.Headers.Add("x-goog-api-key", key.Trim());
        req.Content = Json(new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = messages.Select(m => new { role = m.FromUser ? "user" : "model", parts = new[] { new { text = m.Text } } }),
        });
        var json = await SendAsync(req, ct);
        return string.Concat(json["candidates"]![0]!["content"]!["parts"]!.AsArray().Select(p => (string?)p?["text"]));
    }

    private async Task<string> OpenAiAsync(string system, List<AiMessage> messages, string model, string baseUrl, string? key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl.Trim().TrimEnd('/') + "/chat/completions");
        if (!string.IsNullOrWhiteSpace(key)) req.Headers.Authorization = new("Bearer", key.Trim());
        var all = new List<object> { new { role = "system", content = system } };
        all.AddRange(messages.Select(m => new { role = m.FromUser ? "user" : "assistant", content = m.Text }));
        req.Content = Json(new { model, messages = all, max_tokens = 600 });
        var json = await SendAsync(req, ct);
        return (string?)json["choices"]![0]!["message"]!["content"] ?? "";
    }

    /// <summary>JSON dengan Content-Length (beberapa server lokal tidak menerima body chunked).</summary>
    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");

    private async Task<JsonNode> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using var response = await _http.SendAsync(req, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            string detail = "";
            try
            {
                var err = JsonNode.Parse(body);
                detail = (string?)err?["error"]?["message"] ?? (string?)err?["error"] ?? "";
            }
            catch (JsonException)
            {
            }
            throw new HttpRequestException((int)response.StatusCode switch
            {
                401 or 403 => "Kunci API ditolak",
                404 => "Model atau alamat tidak ditemukan",
                429 => "Batas pemakaian tercapai, coba lagi nanti",
                _ => $"Penyedia AI menolak ({(int)response.StatusCode})",
            } + (detail.Length > 0 ? $": {detail}" : ""));
        }
        return JsonNode.Parse(body) ?? throw new JsonException("jawaban kosong");
    }

    public void Dispose() => _http.Dispose();
}
