namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskAccepted
{
    public string TaskId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string CorrelationId { get; init; } = null!;
}
