using System;
using System.IO;

namespace TiaAgent.AddIn.Bridge;

/// <summary>
/// Configuration for the Bridge HTTP client.
/// Reads from %LOCALAPPDATA%\TiaAgent\addin.json.
/// </summary>
public sealed class BridgeClientConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "addin.json");

    public string BridgeBaseUrl { get; set; } = "http://127.0.0.1:43119";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int PollingIntervalMilliseconds { get; set; } = 500;
    public int TaskTimeoutSeconds { get; set; } = 300;

    public static BridgeClientConfig Load()
    {
        var config = new BridgeClientConfig();

        try
        {
            if (!File.Exists(ConfigFile))
                return config;

            var json = File.ReadAllText(ConfigFile);
            config = ParseJson(json);
        }
        catch
        {
            // Swallow — use defaults
        }

        return config;
    }

    private static BridgeClientConfig ParseJson(string json)
    {
        var config = new BridgeClientConfig();

        var baseUrl = ExtractValue(json, "bridgeBaseUrl");
        if (baseUrl != null)
            config.BridgeBaseUrl = baseUrl;

        var timeout = ExtractIntValue(json, "requestTimeoutSeconds");
        if (timeout.HasValue)
            config.RequestTimeoutSeconds = timeout.Value;

        var polling = ExtractIntValue(json, "pollingIntervalMilliseconds");
        if (polling.HasValue)
            config.PollingIntervalMilliseconds = polling.Value;

        var taskTimeout = ExtractIntValue(json, "taskTimeoutSeconds");
        if (taskTimeout.HasValue)
            config.TaskTimeoutSeconds = taskTimeout.Value;

        return config;
    }

    private static string? ExtractValue(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length) return null;

        if (json[idx] == '"')
        {
            var start = idx + 1;
            var end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        return null;
    }

    private static int? ExtractIntValue(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length) return null;

        var start = idx;
        while (idx < json.Length && char.IsDigit(json[idx])) idx++;

        if (idx > start && int.TryParse(json.Substring(start, idx - start), out var value))
            return value;

        return null;
    }
}
