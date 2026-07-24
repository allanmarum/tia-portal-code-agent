namespace TiaAgent.Contracts.Bridge;

public sealed class SelectionSnapshot
{
    public string Name { get; set; } = null!;
    public string ObjectType { get; set; } = null!;
    public string RuntimeType { get; set; } = null!;
    public string PlcName { get; set; } = null!;
    public string TiaPath { get; set; } = null!;
    public string Language { get; set; } = null!;
}
