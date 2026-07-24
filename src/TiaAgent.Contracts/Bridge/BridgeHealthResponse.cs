namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeHealthResponse
{
    public string Status { get; set; } = null!;
    public string BridgeVersion { get; set; } = null!;
    public bool McpConfigured { get; set; }

    // Runtime-agnostic fields (replacing OpenCode-specific ones)
    /// <summary>
    /// The ID of the configured default runtime (e.g. "mimo", "opencode", "claude").
    /// </summary>
    public string? RuntimeId { get; set; }

    /// <summary>
    /// Human-readable display name of the configured runtime.
    /// </summary>
    public string? RuntimeDisplayName { get; set; }

    /// <summary>
    /// Whether the configured runtime is available and ready.
    /// </summary>
    public bool RuntimeAvailable { get; set; }

    /// <summary>
    /// Version of the configured runtime, if available.
    /// </summary>
    public string? RuntimeVersion { get; set; }

    // Backward compatibility — maps to RuntimeAvailable
    public bool OpenCodeAvailable { get; set; }
    public string OpenCodeVersion { get; set; } = null!;
}
