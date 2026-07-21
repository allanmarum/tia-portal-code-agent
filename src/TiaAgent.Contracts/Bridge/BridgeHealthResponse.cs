namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeHealthResponse
{
    public string Status { get; init; } = null!;
    public string BridgeVersion { get; init; } = null!;
    public bool McpConfigured { get; init; }

    // Runtime-agnostic fields (replacing OpenCode-specific ones)
    /// <summary>
    /// The ID of the configured default runtime (e.g. "mimo", "opencode", "claude").
    /// </summary>
    public string? RuntimeId { get; init; }

    /// <summary>
    /// Human-readable display name of the configured runtime.
    /// </summary>
    public string? RuntimeDisplayName { get; init; }

    /// <summary>
    /// Whether the configured runtime is available and ready.
    /// </summary>
    public bool RuntimeAvailable { get; init; }

    /// <summary>
    /// Version of the configured runtime, if available.
    /// </summary>
    public string? RuntimeVersion { get; init; }

    // Backward compatibility — maps to RuntimeAvailable
    public bool OpenCodeAvailable { get; init; }
    public string OpenCodeVersion { get; init; } = null!;
}
