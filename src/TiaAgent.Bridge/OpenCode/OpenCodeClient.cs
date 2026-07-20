using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TiaAgent.Bridge.OpenCode;

public sealed class OpenCodeClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OpenCodeClient(string baseUrl, TimeSpan timeout)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = timeout };
    }

    public async Task<HealthResponse> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new HealthResponse { Available = false, Error = $"HTTP {(int)response.StatusCode}" };

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HealthResponse { Available = true, RawJson = body };
        }
        catch (Exception ex)
        {
            return new HealthResponse { Available = false, Error = ex.Message };
        }
    }

    public async Task<SessionResponse> CreateSessionAsync(string agentId, string prompt, CancellationToken cancellationToken = default)
    {
        var payload = $"{{\"agent\":\"{EscapeJson(agentId)}\",\"prompt\":\"{EscapeJson(prompt)}\"}}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/sessions", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new SessionResponse
        {
            Success = response.IsSuccessStatusCode,
            SessionId = ExtractField(body, "sessionId"),
            RawJson = body
        };
    }

    public async Task<MessageResponse> SendMessageAsync(string sessionId, string message, CancellationToken cancellationToken = default)
    {
        var payload = $"{{\"message\":\"{EscapeJson(message)}\"}}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/sessions/{sessionId}/messages", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new MessageResponse
        {
            Success = response.IsSuccessStatusCode,
            RawJson = body
        };
    }

    public async Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _httpClient.PostAsync($"{_baseUrl}/sessions/{sessionId}/abort", null, cancellationToken).ConfigureAwait(false);
        }
        catch { }
    }

    public void Dispose() => _httpClient.Dispose();

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static string? ExtractField(string json, string field)
    {
        var search = $"\"{field}\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx) + 1;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        if (idx >= json.Length) return null;
        if (json[idx] == '"')
        {
            idx++;
            var end = json.IndexOf('"', idx);
            if (end < 0) return null;
            return json.Substring(idx, end - idx);
        }
        var valEnd = idx;
        while (valEnd < json.Length && json[valEnd] != ',' && json[valEnd] != '}' && json[valEnd] != ']') valEnd++;
        return json.Substring(idx, valEnd - idx).Trim();
    }

    public sealed class HealthResponse
    {
        public bool Available { get; init; }
        public string? Error { get; init; }
        public string? RawJson { get; init; }
    }

    public sealed class SessionResponse
    {
        public bool Success { get; init; }
        public string? SessionId { get; init; }
        public string? RawJson { get; init; }
    }

    public sealed class MessageResponse
    {
        public bool Success { get; init; }
        public string? RawJson { get; init; }
    }
}
