using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Events;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Client for the OpenCode agent runtime.
/// </summary>
public interface IOpenCodeClient
{
    Task<OpenCodeSessionDto> CreateSessionAsync(CreateOpenCodeSessionRequest request, CancellationToken cancellationToken);
    Task<OpenCodeTaskDto> StartTaskAsync(StartOpenCodeTaskRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<OpenCodeEventDto> WatchTaskAsync(string taskId, CancellationToken cancellationToken);
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}

public class CreateOpenCodeSessionRequest
{
    public required string CorrelationId { get; init; }
    public required string TiaSessionId { get; init; }
    public required string ProjectId { get; init; }
    public string? DefaultAgent { get; init; }
}

public class StartOpenCodeTaskRequest
{
    public required string SessionId { get; init; }
    public required string CorrelationId { get; init; }
    public required string AgentId { get; init; }
    public required string Message { get; init; }
    public string? SelectionToken { get; init; }
}

public class OpenCodeSessionDto
{
    public required string SessionId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public class OpenCodeTaskDto
{
    public required string TaskId { get; init; }
    public required string SessionId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public class OpenCodeEventDto
{
    public required string EventType { get; init; }
    public required string TaskId { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
