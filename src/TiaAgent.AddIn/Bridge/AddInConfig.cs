using System;
using System.IO;

namespace TiaAgent.AddIn.Bridge;

/// <summary>
/// Configuration for the Bridge HTTP client.
/// Discovers the Bridge port and auth token from the runtime manifest
/// and token file written by the supervisor.
/// </summary>
public sealed class AddInConfig
{
    private static readonly string RuntimeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent", "runtime");

    private static readonly string RuntimeManifestPath = Path.Combine(RuntimeDir, "runtime.json");

    // Token file is one directory up from the runtime dir, at %LOCALAPPDATA%\TiaAgent\bridge.token
    private static readonly string TokenFilePath = Path.Combine(
        Path.GetDirectoryName(RuntimeDir)!, "bridge.token");

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

        // Try to discover auth token from token file
        try
        {
            if (File.Exists(TokenFilePath))
            {
                var token = File.ReadAllText(TokenFilePath).Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    AuthToken = token;
                }
            }
        }
        catch
        {
            // File I/O may be restricted in sandbox — requests will fail with 401
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
