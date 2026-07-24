namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Client for the OpenCode agent runtime.
/// </summary>
public interface IOpenCodeClient
{
    Task<OpenCodeSessionDto> CreateSessionAsync(CreateOpenCodeSessionRequest request, CancellationToken cancellationToken);
    Task<OpenCodeTaskDto> StartTaskAsync(StartOpenCodeTaskRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<OpenCodeEventDto>> GetTaskEventsAsync(string taskId, CancellationToken cancellationToken);
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}

public class CreateOpenCodeSessionRequest
{
    public string CorrelationId { get; set; } = null!;
    public string TiaSessionId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string? DefaultAgent { get; set; }
}

public class StartOpenCodeTaskRequest
{
    public string SessionId { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
    public string AgentId { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? SelectionToken { get; set; }
}

public class OpenCodeSessionDto
{
    public string SessionId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class OpenCodeTaskDto
{
    public string TaskId { get; set; } = null!;
    public string SessionId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class OpenCodeEventDto
{
    public string EventType { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string? Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
