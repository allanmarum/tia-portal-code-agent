namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Block interface definition with input/output/variable parameters.
/// </summary>
public class BlockInterfaceDto
{
    public required string ObjectId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<InterfaceParameterDto> InputParams { get; init; }
    public required IReadOnlyList<InterfaceParameterDto> OutputParams { get; init; }
    public required IReadOnlyList<InterfaceParameterDto> InOutParams { get; init; }
    public required IReadOnlyList<InterfaceParameterDto> StaticVars { get; init; }
    public required IReadOnlyList<InterfaceParameterDto> TempVars { get; init; }
    public string? RawInterfaceText { get; init; }
}

public class InterfaceParameterDto
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public string? DefaultValue { get; init; }
    public string? Comment { get; init; }
    public string? Modifier { get; init; }
}
