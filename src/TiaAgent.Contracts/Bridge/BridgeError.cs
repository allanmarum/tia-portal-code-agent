namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeError
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
    public bool Retryable { get; init; }
}
