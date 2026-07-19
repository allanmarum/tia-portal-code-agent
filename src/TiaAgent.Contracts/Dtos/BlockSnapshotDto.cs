namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Full block snapshot including source code and interface. Returned by tia_read_block.
/// </summary>
public class BlockSnapshotDto
{
    public required string ObjectId { get; init; }
    public required string ProjectId { get; init; }
    public string? PlcId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string BlockType { get; init; }
    public required string Language { get; init; }
    public string? SourceCode { get; init; }
    public BlockInterfaceDto? Interface { get; init; }
    public required string ContentHash { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public required DataProvenance Provenance { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public enum DataProvenance
{
    /// <summary>Directly returned by Openness API.</summary>
    Direct,
    /// <summary>Derived from an Openness export.</summary>
    Exported,
    /// <summary>Inferred by local analysis.</summary>
    Inferred,
    /// <summary>Data unavailable.</summary>
    Unavailable
}
