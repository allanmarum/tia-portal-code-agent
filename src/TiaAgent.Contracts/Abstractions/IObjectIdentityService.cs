using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Generates stable, session-scoped object identifiers.
/// </summary>
public interface IObjectIdentityService
{
    string GenerateId(string tiaSessionId, string projectId, string objectType, string name);
    Task<ObjectIdentityDto?> ResolveAsync(string objectId, CancellationToken cancellationToken);
    Task<bool> IsStaleAsync(string objectId, CancellationToken cancellationToken);
    Task InvalidateAllAsync(string tiaSessionId, string reason, CancellationToken cancellationToken);
}

public class ObjectIdentityDto
{
    public required string ObjectId { get; init; }
    public required string TiaSessionId { get; init; }
    public required string ProjectId { get; init; }
    public required string ObjectType { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset LastResolvedAt { get; init; }
}
