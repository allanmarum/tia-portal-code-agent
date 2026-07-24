#if SIEMENS
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.AddIn.Diagnostics;

namespace TiaAgent.AddIn.Runtime;

/// <summary>
/// Discovers runtime services from the supervisor's runtime.json manifest.
/// Uses manual JSON parsing for net48 compatibility (no System.Text.Json).
/// </summary>
public sealed class RuntimeDiscovery
{
    private static readonly string RuntimeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent", "runtime");

    private static readonly string RuntimeManifestPath = Path.Combine(RuntimeDir, "runtime.json");

    /// <summary>
    /// Attempts to load the runtime manifest from disk.
    /// Returns null if the manifest is missing, invalid, or indicates a non-ready state.
    /// </summary>
    public static RuntimeManifest? TryLoad()
    {
        try
        {
            if (!File.Exists(RuntimeManifestPath))
                return null;

            var json = File.ReadAllText(RuntimeManifestPath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var manifest = ParseManifest(json);
            if (manifest == null)
                return null;

            // Validate schema version
            if (manifest.SchemaVersion != 1)
            {
                AddInLogger.Warn($"Runtime manifest has unsupported schema version: {manifest.SchemaVersion}");
                return null;
            }

            // Validate status
            if (manifest.Status != "ready" && manifest.Status != "degraded")
            {
                AddInLogger.Info($"Runtime manifest status is '{manifest.Status}', not ready");
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to load runtime manifest: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validates a service health endpoint and checks its identity.
    /// </summary>
    public static async Task<bool> ValidateServiceHealthAsync(
        string healthUrl,
        string expectedService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(healthUrl))
            return false;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            // Read as bytes and decode as UTF-8 explicitly.
            // On net48, ReadAsStringAsync() defaults to Latin-1 when Content-Type
            // lacks charset, corrupting non-ASCII characters (e.g. ΓöÇ instead of ─).
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var json = Encoding.UTF8.GetString(bytes);

            // Check service identity
            if (!string.IsNullOrEmpty(expectedService))
            {
                var service = ExtractJsonString(json, "service");
                if (service != null && service != expectedService)
                {
                    AddInLogger.Warn($"Health check expected '{expectedService}' but got '{service}'");
                    return false;
                }
            }

            // Check status
            var status = ExtractJsonString(json, "status");
            return status == "healthy" || status == "ok";
        }
        catch (Exception ex)
        {
            AddInLogger.Debug($"Health check failed for {healthUrl}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the path to the runtime manifest file.
    /// </summary>
    public static string GetManifestPath() => RuntimeManifestPath;

    #region Manual JSON Parsing (net48 compatible)

    private static RuntimeManifest? ParseManifest(string json)
    {
        var manifest = new RuntimeManifest();

        manifest.SchemaVersion = ExtractJsonInt(json, "schemaVersion");
        manifest.InstanceId = ExtractJsonString(json, "instanceId");
        manifest.Status = ExtractJsonString(json, "status");
        manifest.SupervisorPid = ExtractJsonInt(json, "supervisorPid");
        manifest.StartedAt = ExtractJsonString(json, "startedAt");
        manifest.UpdatedAt = ExtractJsonString(json, "updatedAt");

        // Parse services
        var servicesJson = ExtractJsonObject(json, "services");
        if (servicesJson != null)
        {
            var bridgeJson = ExtractJsonObject(servicesJson, "bridge");
            if (bridgeJson != null)
            {
                manifest.Bridge = ParseServiceInfo(bridgeJson);
            }

            var opencodeJson = ExtractJsonObject(servicesJson, "opencode");
            if (opencodeJson != null)
            {
                manifest.OpenCode = ParseServiceInfo(opencodeJson);
            }
        }

        return manifest;
    }

    private static ServiceInfo ParseServiceInfo(string json)
    {
        return new ServiceInfo
        {
            Status = ExtractJsonString(json, "status"),
            Pid = ExtractJsonInt(json, "pid"),
            Host = ExtractJsonString(json, "host"),
            Port = ExtractJsonInt(json, "port"),
            BaseUrl = ExtractJsonString(json, "baseUrl"),
            HealthUrl = ExtractJsonString(json, "healthUrl")
        };
    }

    private static string? ExtractJsonString(string json, string key)
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

    private static int ExtractJsonInt(string json, string key, int defaultValue = 0)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return defaultValue;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return defaultValue;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length) return defaultValue;

        var start = idx;
        while (idx < json.Length && char.IsDigit(json[idx])) idx++;

        if (idx > start && int.TryParse(json.Substring(start, idx - start), out var value))
            return value;

        return defaultValue;
    }

    private static string? ExtractJsonObject(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length || json[idx] != '{') return null;

        var depth = 0;
        var start = idx;
        for (var i = idx; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return json.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    #endregion
}
#endif
