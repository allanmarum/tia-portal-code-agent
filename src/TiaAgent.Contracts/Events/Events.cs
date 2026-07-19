namespace TiaAgent.Contracts.Events;

public class TaskStartedEvent
{
    public required string TaskId { get; init; }
    public required string SessionId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public class ToolCallEvent
{
    public required string ToolName { get; init; }
    public required string CorrelationId { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public class ProgressEvent
{
    public int? Percentage { get; init; }
    public string? Message { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public class TaskCompletedEvent
{
    public required string TaskId { get; init; }
    public bool Success { get; init; }
    public string? Result { get; init; }
    public TimeSpan Duration { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
