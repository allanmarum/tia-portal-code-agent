using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.OpenCode.Client;

/// <summary>
/// Configuration for the OpenCode client.
/// </summary>
public class OpenCodeOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:43120";
    public string DefaultAgent { get; set; } = "tia-explain";
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int StartupTimeoutSeconds { get; set; } = 20;
    public string? AuthToken { get; set; }
}

/// <summary>
/// In-memory mock OpenCode client for testing.
/// </summary>
public class MockOpenCodeClient : IOpenCodeClient
{
    private readonly List<OpenCodeEventDto> _events = new();
    private int _taskCounter;

    public bool WasCreateSessionCalled { get; private set; }
    public bool WasStartTaskCalled { get; private set; }
    public string? LastTaskId { get; private set; }

    public Task<OpenCodeSessionDto> CreateSessionAsync(
        CreateOpenCodeSessionRequest request,
        CancellationToken cancellationToken)
    {
        WasCreateSessionCalled = true;

        return Task.FromResult(new OpenCodeSessionDto
        {
            SessionId = $"mock-session-{Guid.NewGuid():N}".Substring(0, 12),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<OpenCodeTaskDto> StartTaskAsync(
        StartOpenCodeTaskRequest request,
        CancellationToken cancellationToken)
    {
        WasStartTaskCalled = true;
        LastTaskId = $"mock-task-{Interlocked.Increment(ref _taskCounter)}";

        return Task.FromResult(new OpenCodeTaskDto
        {
            TaskId = LastTaskId,
            SessionId = request.SessionId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public async IAsyncEnumerable<OpenCodeEventDto> WatchTaskAsync(
        string taskId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // Simulate tool call events
        yield return new OpenCodeEventDto
        {
            EventType = "tool_call",
            TaskId = taskId,
            Message = "Calling tia_get_current_context",
            Timestamp = DateTimeOffset.UtcNow
        };

        yield return new OpenCodeEventDto
        {
            EventType = "tool_call",
            TaskId = taskId,
            Message = "Calling tia_read_block",
            Timestamp = DateTimeOffset.UtcNow
        };

        yield return new OpenCodeEventDto
        {
            EventType = "progress",
            TaskId = taskId,
            Message = "Analysis in progress...",
            Timestamp = DateTimeOffset.UtcNow
        };

        yield return new OpenCodeEventDto
        {
            EventType = "completed",
            TaskId = taskId,
            Message = "Task completed successfully",
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public Task CancelTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
