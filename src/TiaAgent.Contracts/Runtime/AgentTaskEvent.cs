namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// A progress or status event emitted during task execution.
/// Used for streaming updates from the runtime to the Bridge.
/// </summary>
public sealed class AgentTaskEvent
{
    /// <summary>
    /// Event type: "progress", "tool_call", "completed", "error", "thinking".
    /// </summary>
    public string EventType { get; init; } = null!;

    /// <summary>
    /// Human-readable message describing the event.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Optional structured data payload (JSON string).
    /// </summary>
    public string? Data { get; init; }
}
