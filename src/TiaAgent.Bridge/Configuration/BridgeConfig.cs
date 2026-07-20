using System;
using System.IO;

namespace TiaAgent.Bridge.Configuration;

public sealed class BridgeConfig
{
    public int Port { get; init; } = 43119;
    public string OpenCodeBaseUrl { get; init; } = "http://127.0.0.1:43120";
    public int TaskTimeoutSeconds { get; init; } = 300;
    public int MaxConcurrentTasks { get; init; } = 5;
    public long MaxRequestBodyBytes { get; init; } = 1_048_576;

    public static BridgeConfig Load()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
            return new BridgeConfig();

        try
        {
            var json = File.ReadAllText(configPath);
            return Parse(json);
        }
        catch
        {
            return new BridgeConfig();
        }
    }

    private static BridgeConfig Parse(string json)
    {
        int port = 43119;
        string openCodeBaseUrl = "http://127.0.0.1:43120";
        int taskTimeoutSeconds = 300;
        int maxConcurrentTasks = 5;
        long maxRequestBodyBytes = 1_048_576;

        // Manual JSON parsing — no System.Text.Json dependency needed
        port = ExtractInt(json, "Port") ?? port;
        openCodeBaseUrl = ExtractString(json, "OpenCodeBaseUrl") ?? openCodeBaseUrl;
        taskTimeoutSeconds = ExtractInt(json, "TaskTimeoutSeconds") ?? taskTimeoutSeconds;
        maxConcurrentTasks = ExtractInt(json, "MaxConcurrentTasks") ?? maxConcurrentTasks;
        maxRequestBodyBytes = ExtractLong(json, "MaxRequestBodyBytes") ?? maxRequestBodyBytes;

        return new BridgeConfig
        {
            Port = port,
            OpenCodeBaseUrl = openCodeBaseUrl,
            TaskTimeoutSeconds = taskTimeoutSeconds,
            MaxConcurrentTasks = maxConcurrentTasks,
            MaxRequestBodyBytes = maxRequestBodyBytes
        };
    }

    private static string? ExtractString(string json, string key)
    {
        var search = $"\"{key}\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx) + 1;
        idx = json.IndexOf('"', idx) + 1;
        var end = json.IndexOf('"', idx);
        if (end < 0) return null;
        return json.Substring(idx, end - idx);
    }

    private static int? ExtractInt(string json, string key)
    {
        var search = $"\"{key}\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx) + 1;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        var start = idx;
        while (idx < json.Length && char.IsDigit(json[idx])) idx++;
        if (idx == start) return null;
#pragma warning disable CA1846
        return int.TryParse(json.Substring(start, idx - start), out var val) ? val : null;
#pragma warning restore CA1846
    }

    private static long? ExtractLong(string json, string key)
    {
        var search = $"\"{key}\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx) + 1;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        var start = idx;
        while (idx < json.Length && char.IsDigit(json[idx])) idx++;
        if (idx == start) return null;
#pragma warning disable CA1846
        return long.TryParse(json.Substring(start, idx - start), out var val) ? val : null;
#pragma warning restore CA1846
    }

    private static string GetConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TiaAgent", "bridge.json");
    }
}
