namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskRequest
{
    public string ContractVersion { get; set; } = "1.0";
    public string CorrelationId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string AgentId { get; set; } = null!;
    public TiaInstanceSnapshot TiaInstance { get; set; } = null!;
    public ProjectSnapshot Project { get; set; } = null!;
    public SelectionSnapshot Selection { get; set; } = null!;
    public string UserMessage { get; set; } = null!;

    /// <summary>
    /// Optional runtime override (e.g. "mimo", "opencode", "claude").
    /// If omitted, the Bridge uses the configured default runtime.
    /// </summary>
    public string? Runtime { get; set; }
}
