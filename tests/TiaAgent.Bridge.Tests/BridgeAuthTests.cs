using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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

public class BridgeAuthTests : IDisposable
{
    private readonly int _port;
    private readonly TokenProvider _tokenProvider;
    private BridgeController? _controller;
    private HttpClient? _httpClient;

    public BridgeAuthTests()
    {
        _port = 43200 + new Random().Next(100);
        _tokenProvider = new TokenProvider();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _controller?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk_WithoutAuth()
    {
        StartBridge();

        var response = await _httpClient!.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TaskEndpoint_Returns401_WithoutAuthHeader()
    {
        StartBridge();

        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync("/v1/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"errorType\":\"missing\"");
    }

    [Fact]
    public async Task TaskEndpoint_Returns401_WithMalformedHeader()
    {
        StartBridge();
        _httpClient!.DefaultRequestHeaders.Authorization = null;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tasks")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", "Basic dXNlcjpwYXNz");

        var response = await _httpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"errorType\":\"malformed\"");
    }

    [Fact]
    public async Task TaskEndpoint_Returns401_WithEmptyBearerToken()
    {
        StartBridge();
        _httpClient!.DefaultRequestHeaders.Authorization = null;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tasks")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", "Bearer ");

        var response = await _httpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"errorType\":\"malformed\"");
    }

    [Fact]
    public async Task TaskEndpoint_Returns401_WithInvalidToken()
    {
        StartBridge();
        _httpClient!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "totally-wrong-token");

        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/v1/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"errorType\":\"invalid\"");
    }

    [Fact]
    public async Task TaskEndpoint_Returns400_WithValidAuth_ButBadBody()
    {
        StartBridge();
        SetValidAuthToken();

        var content = new StringContent("not-json", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync("/v1/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TaskEndpoint_Returns202_WithValidAuth_AndValidBody()
    {
        StartBridge();
        SetValidAuthToken();

        var body = @"{""contractVersion"":""1.0"",""correlationId"":""test-123"",""action"":""explain"",""agentId"":""tia-explain"",""userMessage"":""test""}";
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync("/v1/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task DiagnosticsEndpoint_ReturnsAuthTokenFingerprint()
    {
        StartBridge();
        SetValidAuthToken();

        var response = await _httpClient!.GetAsync("/diagnostics");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("authTokenFingerprint");
        json.Should().NotContain(_tokenProvider.Token);
    }

    private void StartBridge()
    {
        var config = new BridgeConfig { Port = _port };
        var logger = new BridgeLogger();
        var runtimeConfig = new TiaAgentConfig { DefaultRuntime = "test" };
        var runtimeRegistry = new RuntimeRegistry(runtimeConfig, logger);
        runtimeRegistry.Register(new FakeRuntime("test", "Test Runtime"));
        var taskManager = new TaskManager(runtimeRegistry, 4, logger);

        _controller = new BridgeController(config, logger, _tokenProvider, runtimeRegistry, taskManager);
        _controller.Start();

        _httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
    }

    private void SetValidAuthToken()
    {
        _httpClient!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenProvider.Token);
    }
}

public class JsonDeserializationTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_LowercaseKeys_MapsToPascalCase()
    {
        var json = @"{
            ""contractVersion"":""1.0"",
            ""correlationId"":""corr-123"",
            ""action"":""explain"",
            ""agentId"":""tia-explain"",
            ""userMessage"":""test message"",
            ""project"":{""id"":""proj-1"",""name"":""TestProject"",""path"":""C:\test""},
            ""selection"":{""name"":""Main"",""objectType"":""OB"",""runtimeType"":""FC"",""plcName"":""PLC1"",""tiaPath"":""path"",""language"":""SCL""},
            ""tiaInstance"":{""processId"":1234,""sessionId"":""sess-1"",""version"":""21.0""}
        }";

        var request = JsonSerializer.Deserialize<BridgeTaskRequest>(json, s_options);

        request.Should().NotBeNull();
        request!.Action.Should().Be("explain");
        request.AgentId.Should().Be("tia-explain");
        request.CorrelationId.Should().Be("corr-123");
        request.UserMessage.Should().Be("test message");

        request.Project.Should().NotBeNull();
        request.Project!.Id.Should().Be("proj-1");
        request.Project.Name.Should().Be("TestProject");

        request.Selection.Should().NotBeNull();
        request.Selection!.Name.Should().Be("Main");
        request.Selection.ObjectType.Should().Be("OB");

        request.TiaInstance.Should().NotBeNull();
        request.TiaInstance!.ProcessId.Should().Be(1234);
    }

    [Fact]
    public void Deserialize_MinimalBody_HasNullables()
    {
        var json = @"{""action"":""explain"",""agentId"":""tia-explain"",""correlationId"":""c1"",""userMessage"":""hi""}";

        var request = JsonSerializer.Deserialize<BridgeTaskRequest>(json, s_options);

        request.Should().NotBeNull();
        request!.Action.Should().Be("explain");
        request.Project.Should().BeNull();
        request.Selection.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyObject_ReturnsDefaults()
    {
        var json = "{}";

        var request = JsonSerializer.Deserialize<BridgeTaskRequest>(json, s_options);

        request.Should().NotBeNull();
        request!.Action.Should().BeNull();
        request.Project.Should().BeNull();
    }
}
