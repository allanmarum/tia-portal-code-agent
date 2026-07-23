using System;
using System.IO;

namespace TiaAgent.AddIn.Bridge;

/// <summary>
/// Configuration for the Bridge HTTP client.
/// Discovers the Bridge port from the runtime manifest written by the supervisor.
/// </summary>
public sealed class AddInConfig
{
    private static readonly string RuntimeManifestPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent", "runtime", "runtime.json");

    public string BridgeBaseUrl { get; set; } = "http://127.0.0.1:43119";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int PollingIntervalMilliseconds { get; set; } = 500;
    public int TaskTimeoutSeconds { get; set; } = 300;
    public string? AuthToken { get; set; }

    public AddInConfig()
    {
        // Try to discover Bridge port from runtime manifest
        try
        {
            if (File.Exists(RuntimeManifestPath))
            {
                var json = File.ReadAllText(RuntimeManifestPath);
                var port = ExtractPort(json);
                if (port > 0)
                {
                    BridgeBaseUrl = $"http://127.0.0.1:{port}";
                }
            }
        }
        catch
        {
            // File I/O may be restricted in sandbox — use default port
        }
    }

    private static int ExtractPort(string json)
    {
        // Find "bridge" object, then "port" value
        var bridgeIdx = json.IndexOf("\"bridge\"", StringComparison.OrdinalIgnoreCase);
        if (bridgeIdx < 0) return 0;

        var portIdx = json.IndexOf("\"port\"", bridgeIdx, StringComparison.OrdinalIgnoreCase);
        if (portIdx < 0) return 0;

        var colonIdx = json.IndexOf(':', portIdx);
        if (colonIdx < 0) return 0;

        var start = colonIdx + 1;
        while (start < json.Length && json[start] == ' ') start++;

        var end = start;
        while (end < json.Length && char.IsDigit(json[end])) end++;

        if (end > start && int.TryParse(json.Substring(start, end - start), out var port))
            return port;

        return 0;
    }
}
