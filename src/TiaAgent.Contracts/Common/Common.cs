namespace TiaAgent.Contracts.Common;

/// <summary>
/// Opaque reference to a TIA Portal engineering object.
/// </summary>
public class ObjectReference
{
    public string? TiaSessionId { get; init; }
    public string? ProjectId { get; init; }
    public required string ObjectId { get; init; }
    public string? ObjectType { get; init; }
    public string? ExpectedContentHash { get; init; }
}

/// <summary>
/// Limits for graph traversal and pagination.
/// </summary>
public static class TiaLimits
{
    public const int MaxPageSize = 100;
    public const int MaxHierarchyDepth = 5;
    public const int MaxHierarchyNodes = 500;
    public const int MaxBlockSourceBytes = 524288;
    public const int MaxBlockReadsPerTask = 50;
    public const int MaxResponsePayloadBytes = 1048576;
}
