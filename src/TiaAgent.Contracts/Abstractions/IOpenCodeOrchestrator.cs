namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Orchestrates the full Add-In → OpenCode → MCP roundtrip.
/// Manages session creation, task execution, event streaming, and result collection.
/// </summary>
public interface IOpenCodeOrchestrator
{
    /// <summary>
    /// Checks if the OpenCode server is reachable and healthy.
    /// </summary>
    Task<bool> IsOpenCodeAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes a complete task through OpenCode and returns the collected result.
    /// </summary>
    Task<OpenCodeOrchestratorResult> ExecuteTaskAsync(
        OpenCodeTaskDescriptor descriptor,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes a task to be sent to OpenCode.
/// </summary>
public class OpenCodeTaskDescriptor
{
    /// <summary>Action identifier (e.g., "explain_selected_object", "test_connection", "test_mcp_roundtrip").</summary>
    public required string Action { get; init; }

    /// <summary>The message/prompt to send to the OpenCode agent.</summary>
    public required string Message { get; init; }

    /// <summary>Optional selection token for the selected TIA object.</summary>
    public string? SelectionToken { get; init; }

    /// <summary>Optional metadata about the selected object.</summary>
    public SelectedObjectMetadata? SelectedObject { get; init; }

    /// <summary>Correlation ID for tracing.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>TIA session ID.</summary>
    public required string TiaSessionId { get; init; }

    /// <summary>Project ID.</summary>
    public required string ProjectId { get; init; }

    /// <summary>Agent ID to use in OpenCode (defaults to configured default).</summary>
    public string? AgentId { get; init; }
}

/// <summary>
/// Minimal metadata about the selected TIA object, included in the initial OpenCode request.
/// </summary>
public class SelectedObjectMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ObjectType { get; init; }
    public string? Language { get; init; }
    public string? PlcName { get; init; }
}

/// <summary>
/// Result of an orchestrated OpenCode task.
/// </summary>
public class OpenCodeOrchestratorResult
{
    public bool Success { get; init; }
    public string? Response { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public string? CorrelationId { get; init; }
    public string? SessionId { get; init; }
    public string? TaskId { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = Array.Empty<ToolCallRecord>();
}

/// <summary>
/// Record of a tool call made during task execution.
/// </summary>
public class ToolCallRecord
{
    public required string ToolName { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
}
