using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.OpenCode.Client;

/// <summary>
/// HTTP client implementation for the OpenCode agent runtime.
/// </summary>
public class OpenCodeHttpClient : IOpenCodeClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenCodeOptions _options;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OpenCodeHttpClient(HttpClient httpClient, OpenCodeOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<OpenCodeSessionDto> CreateSessionAsync(
        CreateOpenCodeSessionRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            correlationId = request.CorrelationId,
            tiaSessionId = request.TiaSessionId,
            projectId = request.ProjectId,
            defaultAgent = request.DefaultAgent ?? _options.DefaultAgent
        };

        var response = await PostAsync<OpenCodeSessionDto>("/api/sessions", payload, cancellationToken);
        return response;
    }

    public async Task<OpenCodeTaskDto> StartTaskAsync(
        StartOpenCodeTaskRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            sessionId = request.SessionId,
            correlationId = request.CorrelationId,
            agentId = request.AgentId,
            message = request.Message,
            selectionToken = request.SelectionToken
        };

        var response = await PostAsync<OpenCodeTaskDto>($"/api/sessions/{request.SessionId}/tasks", payload, cancellationToken);
        return response;
    }

    public async IAsyncEnumerable<OpenCodeEventDto> WatchTaskAsync(
        string taskId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tasks/{taskId}/events");
        request.Headers.Add("Accept", "text/event-stream");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var json = line[6..];
                var evt = JsonSerializer.Deserialize<OpenCodeEventDto>(json, s_jsonOptions);
                if (evt != null)
                    yield return evt;
            }
        }
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tasks/{taskId}/cancel");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<T> PostAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(path, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseJson, s_jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {path}");
    }
}
