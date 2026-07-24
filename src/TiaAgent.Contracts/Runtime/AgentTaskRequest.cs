using TiaAgent.Contracts.Bridge;

namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// A task request for a runtime adapter to execute.
/// Runtime-agnostic: contains the prompt and context, not runtime-specific flags.
/// </summary>
public sealed class AgentTaskRequest
{
    /// <summary>
    /// Unique task identifier assigned by the Bridge.
    /// </summary>
    public string TaskId { get; set; } = null!;

    /// <summary>
    /// Correlation ID for traceability across services.
    /// </summary>
    public string CorrelationId { get; set; } = null!;

    /// <summary>
    /// The action type (e.g. "explain", "review", "propose").
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// The agent profile ID (e.g. "tia-explain", "tia-review", "tia-change").
    /// </summary>
    public string AgentId { get; set; } = null!;

    /// <summary>
    /// The fully-built prompt to send to the runtime.
    /// </summary>
    public string Prompt { get; set; } = null!;

    /// <summary>
    /// Optional runtime override from the task request.
    /// If set, takes precedence over the configured default.
    /// </summary>
    public string? RuntimeOverride { get; set; }

    /// <summary>
    /// TIA Portal project context.
    /// </summary>
    public ProjectSnapshot? Project { get; set; }

    /// <summary>
    /// TIA Portal selection context.
    /// </summary>
    public SelectionSnapshot? Selection { get; set; }
}
