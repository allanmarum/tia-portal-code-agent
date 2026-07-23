using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Bridge;

/// <summary>
/// HTTP client for communicating with the TIA Portal Code Agent Bridge.
/// Uses System.Net.Http.HttpClient (available in net48).
/// Manual JSON serialization — no System.Text.Json dependency.
/// </summary>
public sealed class AgentBridgeClient : IAgentBridgeClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AddInConfig _config;
    private readonly bool _ownsHttpClient;

    public AgentBridgeClient(AddInConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.BridgeBaseUrl),
            Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
        };
        ConfigureAuthentication(_httpClient, config);
        _ownsHttpClient = true;
    }

    public AgentBridgeClient(HttpClient httpClient, AddInConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ConfigureAuthentication(_httpClient, config);
        _ownsHttpClient = false;
    }

    private void ConfigureAuthentication(HttpClient client, AddInConfig config)
    {
        if (!string.IsNullOrEmpty(config.AuthToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AuthToken);
            AddInLogger.Info($"Bridge auth configured: Bearer token loaded ({TokenFingerprint(config.AuthToken!)})");
        }
        else
        {
            AddInLogger.Warn("Bridge auth token not found — requests to Bridge will be rejected");
        }
    }

    private static string TokenFingerprint(string token)
    {
        if (string.IsNullOrEmpty(token)) return "<empty>";
        if (token.Length > 8)
            return string.Format("{0}...{1} ({2} chars)", token.Substring(0, 4), token.Substring(token.Length - 4), token.Length);
        return string.Format("{0}... ({1} chars)", token.Substring(0, 2), token.Length);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    public async Task<BridgeHealthResponse> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/health", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ParseHealthResponse(json);
    }

    public async Task<BridgeTaskAccepted> StartTaskAsync(BridgeTaskRequest request, CancellationToken cancellationToken)
    {
        var json = BuildTaskRequestJson(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/v1/tasks", content, cancellationToken).ConfigureAwait(false);
        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new BridgeTaskException($"Bridge returned {(int)response.StatusCode}: {responseJson}");

        return ParseTaskAccepted(responseJson);
    }

    public async Task<BridgeTaskStatus> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/v1/tasks/{taskId}", cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new BridgeTaskException($"Bridge returned {(int)response.StatusCode}: {json}");

        return ParseTaskStatus(json);
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync($"/v1/tasks/{taskId}/cancel", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    #region JSON Serialization (Manual)

    private string BuildTaskRequestJson(BridgeTaskRequest request)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        sb.AppendFormat("\"contractVersion\":\"{0}\"", EscapeJson(request.ContractVersion));
        sb.AppendFormat(",\"correlationId\":\"{0}\"", EscapeJson(request.CorrelationId));
        sb.AppendFormat(",\"action\":\"{0}\"", EscapeJson(request.Action));
        sb.AppendFormat(",\"agentId\":\"{0}\"", EscapeJson(request.AgentId));
        sb.AppendFormat(",\"userMessage\":\"{0}\"", EscapeJson(request.UserMessage));

        if (request.TiaInstance != null)
        {
            sb.Append(",\"tiaInstance\":{");
            sb.AppendFormat("\"processId\":{0}", request.TiaInstance.ProcessId);
            sb.AppendFormat(",\"sessionId\":\"{0}\"", EscapeJson(request.TiaInstance.SessionId));
            sb.AppendFormat(",\"version\":\"{0}\"", EscapeJson(request.TiaInstance.Version));
            sb.Append('}');
        }

        if (request.Project != null)
        {
            sb.Append(",\"project\":{");
            sb.AppendFormat("\"id\":\"{0}\"", EscapeJson(request.Project.Id));
            sb.AppendFormat(",\"name\":\"{0}\"", EscapeJson(request.Project.Name));
            sb.AppendFormat(",\"path\":\"{0}\"", EscapeJson(request.Project.Path));
            sb.Append('}');
        }

        if (request.Selection != null)
        {
            sb.Append(",\"selection\":{");
            sb.AppendFormat("\"name\":\"{0}\"", EscapeJson(request.Selection.Name));
            sb.AppendFormat(",\"objectType\":\"{0}\"", EscapeJson(request.Selection.ObjectType));
            sb.AppendFormat(",\"runtimeType\":\"{0}\"", EscapeJson(request.Selection.RuntimeType));
            sb.AppendFormat(",\"plcName\":\"{0}\"", EscapeJson(request.Selection.PlcName));
            sb.AppendFormat(",\"tiaPath\":\"{0}\"", EscapeJson(request.Selection.TiaPath));
            sb.AppendFormat(",\"language\":\"{0}\"", EscapeJson(request.Selection.Language));
            sb.Append('}');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJson(string? value)
    {
        if (value == null) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
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

    private static bool ExtractJsonBool(string json, string key, bool defaultValue = false)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return defaultValue;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return defaultValue;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx + 4 <= json.Length && json.Substring(idx, 4) == "true")
            return true;
        if (idx + 5 <= json.Length && json.Substring(idx, 5) == "false")
            return false;

        return defaultValue;
    }

    private BridgeHealthResponse ParseHealthResponse(string json)
    {
        return new BridgeHealthResponse
        {
            Status = ExtractJsonString(json, "status") ?? "unknown",
            BridgeVersion = ExtractJsonString(json, "bridgeVersion") ?? "unknown",
            McpConfigured = ExtractJsonBool(json, "mcpConfigured"),
            // New runtime fields (backward compatible)
            RuntimeId = ExtractJsonString(json, "runtimeId"),
            RuntimeDisplayName = ExtractJsonString(json, "runtimeDisplayName"),
            RuntimeAvailable = ExtractJsonBool(json, "runtimeAvailable"),
            RuntimeVersion = ExtractJsonString(json, "runtimeVersion"),
            // Legacy fields (map to runtime fields for backward compat)
            OpenCodeAvailable = ExtractJsonBool(json, "openCodeAvailable") || ExtractJsonBool(json, "runtimeAvailable"),
            OpenCodeVersion = ExtractJsonString(json, "openCodeVersion") ?? ExtractJsonString(json, "runtimeVersion") ?? ""
        };
    }

    private BridgeTaskAccepted ParseTaskAccepted(string json)
    {
        return new BridgeTaskAccepted
        {
            TaskId = ExtractJsonString(json, "taskId") ?? "",
            Status = ExtractJsonString(json, "status") ?? "pending",
            CorrelationId = ExtractJsonString(json, "correlationId") ?? ""
        };
    }

    private BridgeTaskStatus ParseTaskStatus(string json)
    {
        var errorJson = ExtractJsonObject(json, "error");
        BridgeError? error = null;
        if (errorJson != null)
        {
            error = new BridgeError
            {
                Code = ExtractJsonString(errorJson, "code") ?? "",
                Message = ExtractJsonString(errorJson, "message") ?? "",
                Retryable = ExtractJsonBool(errorJson, "retryable")
            };
        }

        return new BridgeTaskStatus
        {
            TaskId = ExtractJsonString(json, "taskId") ?? "",
            Status = ExtractJsonString(json, "status") ?? "",
            Stage = ExtractJsonString(json, "stage") ?? "",
            Message = ExtractJsonString(json, "message") ?? "",
            RuntimeId = ExtractJsonString(json, "runtimeId"),
            RuntimeVersion = ExtractJsonString(json, "runtimeVersion"),
            Response = ExtractJsonString(json, "response") ?? "",
            Error = error
        };
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

/// <summary>
/// Exception thrown when a Bridge task operation fails.
/// </summary>
public sealed class BridgeTaskException : Exception
{
    public BridgeTaskException(string message) : base(message) { }
    public BridgeTaskException(string message, Exception inner) : base(message, inner) { }
}
