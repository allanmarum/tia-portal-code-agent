namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeError
{
    public string Code { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool Retryable { get; set; }
}
