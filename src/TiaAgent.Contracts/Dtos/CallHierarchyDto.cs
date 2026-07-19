namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Hierarchical call relationship tree. Returned by tia_get_call_hierarchy.
/// </summary>
public class CallHierarchyDto
{
    public required string RootObjectId { get; init; }
    public required string RootName { get; init; }
    public required IReadOnlyList<CallHierarchyNodeDto> Nodes { get; init; }
    public bool IsPartial { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public class CallHierarchyNodeDto
{
    public required string ObjectId { get; init; }
    public required string Name { get; init; }
    public required string BlockType { get; init; }
    public string? Path { get; init; }
    public required IReadOnlyList<CallHierarchyNodeDto> Children { get; init; }
}
