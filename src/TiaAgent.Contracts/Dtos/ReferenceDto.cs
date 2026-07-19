namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Reference search result. Returned by tia_find_references.
/// </summary>
public class ReferenceSearchResultDto
{
    public required IReadOnlyList<ReferenceDto> References { get; init; }
    public bool IsPartial { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public class ReferenceDto
{
    public required string SourceObjectId { get; init; }
    public required string SourceName { get; init; }
    public required string TargetObjectId { get; init; }
    public required string TargetName { get; init; }
    public required string ReferenceType { get; init; }
    public string? Location { get; init; }
}
