using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TiaAgent.Contracts.Bridge;
using TiaAgent.ResponseCenter.Models;
using TiaAgent.ResponseCenter.Services;
using Xunit;

namespace TiaAgent.ResponseCenter.Tests;

public class BridgeTaskMonitorTests
{
    private static AgentResponseContext CreateContext(string taskId = "task123", string? bridgeUrl = null)
    {
        return new AgentResponseContext
        {
            TaskId = taskId,
            BridgeUrl = bridgeUrl ?? "http://localhost:9999",
            Action = "explain",
            ObjectName = "FB_Test",
            ObjectType = "Function Block",
            CorrelationId = "tia-test123"
        };
    }

    [Fact]
    public void Constructor_AcceptsValidContext()
    {
        var ctx = CreateContext();
        using var monitor = new BridgeTaskMonitor(ctx);
        monitor.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ctx = CreateContext();
        var monitor = new BridgeTaskMonitor(ctx);
        monitor.Dispose();
    }

    [Fact]
    public void MapBridgeStatus_MapsAllStatuses()
    {
        BridgeTaskStatusValues.Pending.Should().Be("pending");
        BridgeTaskStatusValues.Running.Should().Be("running");
        BridgeTaskStatusValues.Completed.Should().Be("completed");
        BridgeTaskStatusValues.Failed.Should().Be("failed");
        BridgeTaskStatusValues.Cancelled.Should().Be("cancelled");
        BridgeTaskStatusValues.WaitingForApproval.Should().Be("waiting_for_approval");
    }

    [Fact]
    public async Task Monitor_TransitionsToCompleted_WhenBridgeReturnsCompleted()
    {
        var responses = new Queue<string>();
        responses.Enqueue(JsonSerializer.Serialize(new BridgeTaskStatus
        {
            TaskId = "task123",
            Status = "running",
            Stage = "executing",
            Message = "Working..."
        }));
        responses.Enqueue(JsonSerializer.Serialize(new BridgeTaskStatus
        {
            TaskId = "task123",
            Status = "completed",
            Stage = "done",
            Response = "Here is the result.",
            RuntimeId = "mimo"
        }));

        var handler = new MockHttpHandler(responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9999/") };

        var ctx = CreateContext();
        var monitor = new BridgeTaskMonitor(ctx, pollingInterval: TimeSpan.FromMilliseconds(50), httpClient: httpClient);

        var states = new List<AgentTaskState>();
        var response = "";

        monitor.StateChanged += (state, stage, msg) => states.Add(state);
        monitor.ResponseReceived += r => response = r;

        monitor.Start();
        await Task.Delay(1000);
        monitor.Stop();

        states.Should().Contain(AgentTaskState.Running);
        states.Should().Contain(AgentTaskState.Completed);
        response.Should().Be("Here is the result.");

        monitor.Dispose();
        handler.Dispose();
    }

    [Fact]
    public async Task Monitor_TransitionsToFailed_WhenBridgeReturnsFailed()
    {
        var responses = new Queue<string>();
        responses.Enqueue(JsonSerializer.Serialize(new BridgeTaskStatus
        {
            TaskId = "task123",
            Status = "failed",
            Error = new BridgeError
            {
                Code = "OPENCODE_TASK_FAILED",
                Message = "Runtime crashed",
                Retryable = true
            }
        }));

        var handler = new MockHttpHandler(responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9999/") };

        var ctx = CreateContext();
        var monitor = new BridgeTaskMonitor(ctx, pollingInterval: TimeSpan.FromMilliseconds(50), httpClient: httpClient);

        var states = new List<AgentTaskState>();
        string? errorMsg = null;
        bool retryable = false;

        monitor.StateChanged += (state, stage, msg) => states.Add(state);
        monitor.ErrorOccurred += (msg, code, tech, r) => { errorMsg = msg; retryable = r; };

        monitor.Start();
        await Task.Delay(1000);
        monitor.Stop();

        states.Should().Contain(AgentTaskState.Failed);
        errorMsg.Should().Be("Runtime crashed");
        retryable.Should().BeTrue();

        monitor.Dispose();
        handler.Dispose();
    }

    [Fact]
    public async Task Monitor_TransitionsToCancelled_WhenBridgeReturnsCancelled()
    {
        var responses = new Queue<string>();
        responses.Enqueue(JsonSerializer.Serialize(new BridgeTaskStatus
        {
            TaskId = "task123",
            Status = "cancelled"
        }));

        var handler = new MockHttpHandler(responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9999/") };

        var ctx = CreateContext();
        var monitor = new BridgeTaskMonitor(ctx, pollingInterval: TimeSpan.FromMilliseconds(50), httpClient: httpClient);

        var states = new List<AgentTaskState>();
        monitor.StateChanged += (state, stage, msg) => states.Add(state);

        monitor.Start();
        await Task.Delay(1000);
        monitor.Stop();

        states.Should().Contain(AgentTaskState.Cancelled);

        monitor.Dispose();
        handler.Dispose();
    }

    [Fact]
    public async Task Monitor_TransitionsToDisconnected_AfterConsecutiveErrors()
    {
        var handler = new MockHttpHandler(new Queue<string>(), alwaysFail: true);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9999/") };

        var ctx = CreateContext();
        var monitor = new BridgeTaskMonitor(ctx,
            pollingInterval: TimeSpan.FromMilliseconds(50),
            taskTimeout: TimeSpan.FromSeconds(30),
            httpClient: httpClient);

        var states = new List<AgentTaskState>();
        monitor.StateChanged += (state, stage, msg) => states.Add(state);

        monitor.Start();
        await Task.Delay(5000);
        monitor.Stop();

        states.Should().Contain(AgentTaskState.Disconnected);

        monitor.Dispose();
        handler.Dispose();
    }

    [Fact]
    public async Task Monitor_HandlesInitialStatus()
    {
        var responses = new Queue<string>();
        // Return an empty JSON object so the monitor doesn't crash
        responses.Enqueue("{}");

        var handler = new MockHttpHandler(responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9999/") };

        var ctx = CreateContext();
        ctx = ctx with { InitialStatus = "running", InitialStage = "executing" };

        var monitor = new BridgeTaskMonitor(ctx, pollingInterval: TimeSpan.FromMilliseconds(50), httpClient: httpClient);

        AgentTaskState? initialState = null;
        monitor.StateChanged += (state, stage, msg) =>
        {
            if (initialState == null)
                initialState = state;
        };

        monitor.Start();
        await Task.Delay(200);
        monitor.Stop();

        initialState.Should().Be(AgentTaskState.Running);

        monitor.Dispose();
        handler.Dispose();
    }
}

/// <summary>
/// Mock HTTP handler that returns pre-configured responses.
/// </summary>
internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;
    private readonly bool _alwaysFail;

    public MockHttpHandler(Queue<string> responses, bool alwaysFail = false)
    {
        _responses = responses;
        _alwaysFail = alwaysFail;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_alwaysFail)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Service unavailable")
            });
        }

        if (_responses.Count > 0)
        {
            var json = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
    }
}
