namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskAccepted
{
    public string TaskId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
}
