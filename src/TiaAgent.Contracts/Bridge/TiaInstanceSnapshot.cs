namespace TiaAgent.Contracts.Bridge;

public sealed class TiaInstanceSnapshot
{
    public int ProcessId { get; init; }
    public string SessionId { get; init; } = null!;
    public string Version { get; init; } = null!;
}
