namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Paginated result wrapper for list operations.
/// </summary>
public class PagedResultDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool IsPartial { get; init; }
    public int? TotalCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
