using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TiaAgent.Bridge.Api;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Tasks;
using TiaAgent.Contracts.Bridge;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Bridge.Tests;

public class TaskRoutingTests : IDisposable
{
    private readonly int _port;
    private readonly TokenProvider _tokenProvider;
    private readonly BridgeLogger _logger;
    private readonly RuntimeRegistry _runtimeRegistry;
    private BridgeController? _controller;
    private HttpClient? _httpClient;

    public TaskRoutingTests()
    {
        _port = 43300 + new Random().Next(100);
        _tokenProvider = new TokenProvider();
        _logger = new BridgeLogger();
        var runtimeConfig = new TiaAgentConfig { DefaultRuntime = "test" };
        _runtimeRegistry = new RuntimeRegistry(runtimeConfig, _logger);
        _runtimeRegistry.Register(new FakeRuntime("test", "Test Runtime"));
        _runtimeRegistry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        _runtimeRegistry.Register(new FakeRuntime("claude", "Claude Code CLI"));
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _controller?.Dispose();
        _runtimeRegistry.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateTask_WithRuntimeOverride_RoutesToCorrectRuntime()
    {
        StartBridge();
        SetValidAuthToken();

        var body = @"{""contractVersion"":""1.0"",""correlationId"":""test-123"",""action"":""explain"",""agentId"":""tia-explain"",""userMessage"":""test"",""runtime"":""mimo""}";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync("/v1/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CreateTask_WithoutRuntimeOverride_UsesDefault()
    {
        StartBridge();
        SetValidAuthToken();

        var body = @"{""contractVersion"":""1.0"",""correlationId"":""test-123"",""action"":""explain"",""agentId"":""tia-explain"",""userMessage"":""test""}";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync("/v1/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task TaskStatus_ContainsRuntimeMetadata()
    {
        StartBridge();
        SetValidAuthToken();

        // Create a task
        var body = @"{""contractVersion"":""1.0"",""correlationId"":""test-123"",""action"":""explain"",""agentId"":""tia-explain"",""userMessage"":""test""}";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var createResponse = await _httpClient!.PostAsync("/v1/tasks", content);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var createJson = await createResponse.Content.ReadAsStringAsync();
        var taskId = ExtractJsonString(createJson, "taskId");
        taskId.Should().NotBeNullOrEmpty();

        // Wait a moment for the task to execute (fake runtime is instant)
        await Task.Delay(200);

        // Get task status
        var statusResponse = await _httpClient.GetAsync($"/v1/tasks/{taskId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusJson = await statusResponse.Content.ReadAsStringAsync();
        statusJson.Should().Contain("\"runtimeId\"");
    }

    [Fact]
    public async Task ListRuntimes_ReturnsAllRegisteredRuntimes()
    {
        StartBridge();
        SetValidAuthToken();

        var response = await _httpClient!.GetAsync("/api/runtimes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("test");
        json.Should().Contain("mimo");
        json.Should().Contain("claude");
        json.Should().Contain("\"default\"");
    }

    [Fact]
    public async Task RuntimeHealth_ReturnsAvailabilityInfo()
    {
        StartBridge();
        SetValidAuthToken();

        var response = await _httpClient!.GetAsync("/api/runtimes/test/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"available\":true");
        json.Should().Contain("\"id\":\"test\"");
    }

    [Fact]
    public async Task RuntimeHealth_UnknownRuntime_Returns404()
    {
        StartBridge();
        SetValidAuthToken();

        var response = await _httpClient!.GetAsync("/api/runtimes/nonexistent/health");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRuntimeSettings_ReturnsDefaultRuntime()
    {
        StartBridge();
        SetValidAuthToken();

        var response = await _httpClient!.GetAsync("/api/settings/runtime");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"defaultRuntime\":\"test\"");
    }

    [Fact]
    public async Task HealthEndpoint_IncludesRuntimeInfo()
    {
        StartBridge();

        var response = await _httpClient!.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"runtimeId\"");
        json.Should().Contain("\"runtimeAvailable\"");
    }

    [Fact]
    public async Task CreateTask_UnknownRuntime_ReturnsError()
    {
        StartBridge();
        SetValidAuthToken();

        var body = @"{""contractVersion"":""1.0"",""correlationId"":""test-123"",""action"":""explain"",""agentId"":""tia-explain"",""userMessage"":""test"",""runtime"":""nonexistent""}";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var createResponse = await _httpClient!.PostAsync("/v1/tasks", content);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Wait for task to fail
        await Task.Delay(200);

        var createJson = await createResponse.Content.ReadAsStringAsync();
        var taskId = ExtractJsonString(createJson, "taskId");

        var statusResponse = await _httpClient.GetAsync($"/v1/tasks/{taskId}");
        var statusJson = await statusResponse.Content.ReadAsStringAsync();
        statusJson.Should().Contain("RUNTIME_NOT_FOUND");
        statusJson.Should().Contain("nonexistent");
    }

    private void StartBridge()
    {
        var config = new BridgeConfig { Port = _port };
        var taskManager = new TaskManager(_runtimeRegistry, 4, _logger);

        _controller = new BridgeController(config, _logger, _tokenProvider, _runtimeRegistry, taskManager);
        _controller.Start();

        _httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
    }

    private void SetValidAuthToken()
    {
        _httpClient!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenProvider.Token);
    }

    private static string? ExtractJsonString(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx) + 1;
        while (idx < json.Length && json[idx] == ' ') idx++;
        if (idx >= json.Length || json[idx] != '"') return null;
        idx++;
        var end = json.IndexOf('"', idx);
        if (end < 0) return null;
        return json.Substring(idx, end - idx);
    }
}
