using System.Threading;
using System.Threading.Tasks;

namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// Abstraction for an interchangeable coding agent runtime (Mimo, OpenCode, Claude Code, etc.).
/// Each runtime adapter implements this interface to provide a uniform execution contract
/// without exposing runtime-specific commands, flags, or process management.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Stable identifier for this runtime (e.g. "mimo", "opencode", "claude").
    /// Used in configuration, environment variables, and task requests.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name for display purposes (e.g. "Mimo CLI", "Claude Code CLI").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Checks whether this runtime is installed and ready to accept tasks.
    /// Does not start the runtime; just verifies prerequisites.
    /// </summary>
    Task<RuntimeAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes a TIA Portal task using this runtime.
    /// The runtime receives a prompt and returns a structured result.
    /// </summary>
    Task<AgentTaskResult> ExecuteAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a running task. Best-effort; may not be supported by all runtimes.
    /// </summary>
    Task CancelAsync(string taskId, CancellationToken cancellationToken);
}
