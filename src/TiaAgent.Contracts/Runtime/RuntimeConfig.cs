using System.Collections.Generic;

namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// Top-level configuration for TIA Agent runtime selection.
/// Loaded from %LOCALAPPDATA%\TiaAgent\config.json.
/// </summary>
public sealed class TiaAgentConfig
{
    /// <summary>
    /// The default runtime ID to use when no override is specified.
    /// </summary>
    public string DefaultRuntime { get; init; } = "opencode";

    /// <summary>
    /// Per-runtime configuration entries.
    /// </summary>
    public Dictionary<string, RuntimeEntryConfig> Runtimes { get; init; } = new Dictionary<string, RuntimeEntryConfig>();
}

/// <summary>
/// Configuration for a single runtime entry.
/// </summary>
public sealed class RuntimeEntryConfig
{
    /// <summary>
    /// Whether this runtime is enabled and available for selection.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Path to the runtime executable. If null, the runtime is expected to be on PATH.
    /// </summary>
    public string? Executable { get; init; }

    /// <summary>
    /// The mode to use: "server" or "cli". Not all runtimes support both.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>
    /// The server URL when in server mode (e.g. "http://127.0.0.1:43120").
    /// </summary>
    public string? ServerUrl { get; init; }

    /// <summary>
    /// Additional environment variables to set when launching the runtime process.
    /// </summary>
    public Dictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}
