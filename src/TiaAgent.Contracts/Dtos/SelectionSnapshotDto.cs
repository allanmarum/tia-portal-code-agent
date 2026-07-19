namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Immutable snapshot of a user's selection at the moment a command was triggered.
/// </summary>
public class SelectionSnapshotDto
{
    public required string SelectionToken { get; init; }
    public required string TiaSessionId { get; init; }
    public required string ProjectId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public required IReadOnlyList<SelectedObjectDto> Objects { get; init; }
    public string? ProjectName { get; init; }
}

public class SelectedObjectDto
{
    public required string ObjectId { get; init; }
    public required string NameAtCapture { get; init; }
    public required string PathAtCapture { get; init; }
    public required string ObjectType { get; init; }
    public string? PlcId { get; init; }
    public string? PlcName { get; init; }
    public string? ContentHash { get; init; }
}
