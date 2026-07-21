namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// The result of executing a task through a runtime adapter.
/// </summary>
public sealed class AgentTaskResult
{
    /// <summary>
    /// Whether the task completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The agent's response text (on success).
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// Human-readable error message (on failure).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Structured error code (e.g. "RUNTIME_UNAVAILABLE", "TASK_TIMEOUT").
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// The ID of the runtime that executed this task.
    /// </summary>
    public string RuntimeId { get; init; } = null!;

    /// <summary>
    /// The version of the runtime that executed this task.
    /// </summary>
    public string? RuntimeVersion { get; init; }

    /// <summary>
    /// The active mode used (e.g. "cli", "server").
    /// </summary>
    public string? RuntimeMode { get; init; }
}
