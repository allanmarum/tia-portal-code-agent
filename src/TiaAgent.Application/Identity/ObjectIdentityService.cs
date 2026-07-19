using System.Collections.Concurrent;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Application.Identity;

public class ObjectIdentityService : IObjectIdentityService
{
    private readonly ConcurrentDictionary<string, ObjectIdentityDto> _identities = new();
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public ObjectIdentityService(IIdGenerator idGenerator, IClock clock)
    {
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public string GenerateId(string tiaSessionId, string projectId, string objectType, string name)
    {
        var id = _idGenerator.NewId();
        var identity = new ObjectIdentityDto
        {
            ObjectId = id,
            TiaSessionId = tiaSessionId,
            ProjectId = projectId,
            ObjectType = objectType,
            Name = name,
            LastResolvedAt = _clock.UtcNow
        };
        _identities[id] = identity;
        return id;
    }

    public Task<ObjectIdentityDto?> ResolveAsync(string objectId, CancellationToken cancellationToken)
    {
        _identities.TryGetValue(objectId, out var identity);
        return Task.FromResult<ObjectIdentityDto?>(identity);
    }

    public Task<bool> IsStaleAsync(string objectId, CancellationToken cancellationToken)
    {
        if (!_identities.TryGetValue(objectId, out var identity))
            return Task.FromResult(true);

        var age = _clock.UtcNow - identity.LastResolvedAt;
        return Task.FromResult(age > TimeSpan.FromMinutes(30));
    }

    public Task InvalidateAllAsync(string tiaSessionId, string reason, CancellationToken cancellationToken)
    {
        var keysToRemove = _identities
            .Where(kvp => kvp.Value.TiaSessionId == tiaSessionId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _identities.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
