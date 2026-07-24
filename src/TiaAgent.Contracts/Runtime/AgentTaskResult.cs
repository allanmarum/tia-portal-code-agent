namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// The result of executing a task through a runtime adapter.
/// </summary>
public sealed class AgentTaskResult
{
    /// <summary>
    /// Whether the task completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The agent's response text (on success).
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Human-readable error message (on failure).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Structured error code (e.g. "RUNTIME_UNAVAILABLE", "TASK_TIMEOUT").
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// The ID of the runtime that executed this task.
    /// </summary>
    public string RuntimeId { get; set; } = null!;

    /// <summary>
    /// The version of the runtime that executed this task.
    /// </summary>
    public string? RuntimeVersion { get; set; }

    /// <summary>
    /// The active mode used (e.g. "cli", "server").
    /// </summary>
    public string? RuntimeMode { get; set; }
}
