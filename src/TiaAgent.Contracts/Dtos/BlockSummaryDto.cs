namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Lightweight block summary for listings. Does NOT include source code.
/// </summary>
public class BlockSummaryDto
{
    public required string ObjectId { get; init; }
    public required string Name { get; init; }
    public required string BlockType { get; init; }
    public required string Path { get; init; }
    public required string Language { get; init; }
    public string? PlcId { get; init; }
    public string? ContentHash { get; init; }
    public DateTimeOffset LastObservedAt { get; init; }
}
