namespace TiaAgent.Contracts.Bridge;

public sealed class TiaInstanceSnapshot
{
    public int ProcessId { get; set; }
    public string SessionId { get; set; } = null!;
    public string Version { get; set; } = null!;
}
