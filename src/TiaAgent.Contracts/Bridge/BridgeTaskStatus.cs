namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskStatus
{
    public string TaskId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string Stage { get; init; } = null!;
    public string Message { get; init; } = null!;
    public string Response { get; init; } = null!;
    public BridgeError? Error { get; init; }
}
