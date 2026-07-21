namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskRequest
{
    public string ContractVersion { get; init; } = "1.0";
    public string CorrelationId { get; init; } = null!;
    public string Action { get; init; } = null!;
    public string AgentId { get; init; } = null!;
    public TiaInstanceSnapshot TiaInstance { get; init; } = null!;
    public ProjectSnapshot Project { get; init; } = null!;
    public SelectionSnapshot Selection { get; init; } = null!;
    public string UserMessage { get; init; } = null!;

    /// <summary>
    /// Optional runtime override (e.g. "mimo", "opencode", "claude").
    /// If omitted, the Bridge uses the configured default runtime.
    /// </summary>
    public string? Runtime { get; init; }
}
