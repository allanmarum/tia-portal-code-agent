namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// Result of a runtime availability check.
/// </summary>
public sealed class RuntimeAvailabilityResult
{
    /// <summary>
    /// Whether the runtime is installed and ready to accept tasks.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Path to the runtime executable, if discovered.
    /// </summary>
    public string? Executable { get; set; }

    /// <summary>
    /// Version string from the runtime, if available.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Human-readable error message when the runtime is unavailable.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The active mode for this runtime (e.g. "cli", "server").
    /// </summary>
    public string? Mode { get; set; }
}
