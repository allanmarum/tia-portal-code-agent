namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeHealthResponse
{
    public string Status { get; init; } = null!;
    public string BridgeVersion { get; init; } = null!;
    public bool OpenCodeAvailable { get; init; }
    public string OpenCodeVersion { get; init; } = null!;
    public bool McpConfigured { get; init; }
}
