using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Peeko.App.Settings;
using Peeko.Platform.Network;

namespace Peeko.App.Sistem;

/// <summary>
/// Claude Code di notch (mati dari awal). Menyalakan <see cref="ClaudeBridge"/> dan memasang/melepas
/// hook di %USERPROFILE%\.claude\settings.json atas permintaan pengguna. Hook yang dipasang Peeko
/// dikenali dari alamatnya (127.0.0.1:47821/claude), jadi hook lain milik pengguna tidak disentuh.
/// </summary>
public sealed class ClaudeCodeService : IDisposable
{
    private const string Marker = "127.0.0.1:47821/claude";
    private readonly SettingsService _settings;

    public ClaudeCodeService(SettingsService settings)
    {
        _settings = settings;
    }

    public ClaudeBridge Bridge { get; } = new();

    public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public bool PortBusy { get; private set; }

    public void Apply()
    {
        if (_settings.Current.ClaudeCodeEnabled)
        {
            PortBusy = !Bridge.Start(Token());
        }
        else
        {
            Bridge.Stop();
            PortBusy = false;
        }
    }

    private string Token()
    {
        if (_settings.Current.ClaudeToken is { Length: >= 16 } t) return t;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _settings.Update(s => s.ClaudeToken = token);
        return token;
    }

    public bool HooksInstalled()
    {
        try
        {
            return File.Exists(SettingsPath) && File.ReadAllText(SettingsPath).Contains(Marker);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string Command(string endpoint, int seconds) =>
        $"curl -s -m {seconds} -X POST -H \"X-Peeko: {Token()}\" -H \"Content-Type: application/json\" --data-binary @- http://{Marker}/{endpoint}";

    /// <summary>Pasang hook Notification, Stop, dan PermissionRequest. File lama dicadangkan dulu.</summary>
    public void InstallHooks()
    {
        var root = Load();
        RemoveOurs(root);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        root["hooks"] = hooks;
        void Add(string ev, string endpoint, int seconds)
        {
            var list = hooks[ev] as JsonArray ?? new JsonArray();
            hooks[ev] = list;
            list.Add(new JsonObject
            {
                ["matcher"] = "",
                ["hooks"] = new JsonArray { new JsonObject { ["type"] = "command", ["command"] = Command(endpoint, seconds), ["timeout"] = seconds + 5 } },
            });
        }
        Add("Notification", "notify", 3);
        Add("Stop", "notify", 3);
        Add("PermissionRequest", "permission", (int)ClaudeBridge.AnswerTimeout.TotalSeconds + 5);
        Save(root);
    }

    public void UninstallHooks()
    {
        if (!File.Exists(SettingsPath)) return;
        var root = Load();
        RemoveOurs(root);
        Save(root);
    }

    private static void RemoveOurs(JsonObject root)
    {
        if (root["hooks"] is not JsonObject hooks) return;
        foreach (var ev in hooks.Select(h => h.Key).ToList())
        {
            if (hooks[ev] is not JsonArray groups) continue;
            for (int i = groups.Count - 1; i >= 0; i--)
                if (groups[i]?.ToJsonString().Contains(Marker) == true) groups.RemoveAt(i);
            if (groups.Count == 0) hooks.Remove(ev);
        }
        if (hooks.Count == 0) root.Remove("hooks");
    }

    private static JsonObject Load()
    {
        if (!File.Exists(SettingsPath)) return new JsonObject();
        var text = File.ReadAllText(SettingsPath);
        return string.IsNullOrWhiteSpace(text)
            ? new JsonObject()
            : JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
              ?? throw new InvalidDataException("settings.json Claude Code bukan objek JSON");
    }

    private static void Save(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        if (File.Exists(SettingsPath)) File.Copy(SettingsPath, SettingsPath + ".sebelum-peeko", overwrite: true);
        File.WriteAllText(SettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Dispose() => Bridge.Dispose();
}
