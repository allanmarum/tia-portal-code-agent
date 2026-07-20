namespace TiaAgent.Contracts.Bridge;

public sealed class SelectionSnapshot
{
    public string Name { get; init; } = null!;
    public string ObjectType { get; init; } = null!;
    public string RuntimeType { get; init; } = null!;
    public string PlcName { get; init; } = null!;
    public string TiaPath { get; init; } = null!;
    public string Language { get; init; } = null!;
}
