namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Writes structured audit events for traceability.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

public class AuditEvent
{
    public required string EventType { get; init; }
    public required string CorrelationId { get; init; }
    public string? TiaSessionId { get; init; }
    public string? ProjectId { get; init; }
    public string? SelectionToken { get; init; }
    public string? ChangeSetId { get; init; }
    public string? ToolName { get; init; }
    public string? ObjectId { get; init; }
    public bool? Success { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
