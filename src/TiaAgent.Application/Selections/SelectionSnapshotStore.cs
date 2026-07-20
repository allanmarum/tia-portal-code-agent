using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Application.Selections;

/// <summary>
/// In-memory selection snapshot store with time-based expiration.
/// </summary>
public class SelectionSnapshotStore : ISelectionSnapshotStore
{
    private readonly ConcurrentDictionary<string, SelectionSnapshotDto> _snapshots = new();
    private readonly IClock _clock;

    public SelectionSnapshotStore(IClock clock)
    {
        _clock = clock;
    }

    public Task<SelectionSnapshotDto?> GetAsync(string selectionToken, CancellationToken cancellationToken)
    {
        _snapshots.TryGetValue(selectionToken, out var snapshot);

        if (snapshot != null && snapshot.ExpiresAt <= _clock.UtcNow)
        {
            _snapshots.TryRemove(selectionToken, out _);
            snapshot = null;
        }

        return Task.FromResult(snapshot);
    }

    public Task SaveAsync(SelectionSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        _snapshots[snapshot.SelectionToken] = snapshot;
        return Task.CompletedTask;
    }

    public Task ExpireAsync(string selectionToken, string reason, CancellationToken cancellationToken)
    {
        _snapshots.TryRemove(selectionToken, out _);
        return Task.CompletedTask;
    }

    public Task ExpireAllForSessionAsync(string tiaSessionId, string reason, CancellationToken cancellationToken)
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _snapshots)
        {
            if (kvp.Value.TiaSessionId == tiaSessionId)
                keysToRemove.Add(kvp.Key);
        }

        foreach (var key in keysToRemove)
            _snapshots.TryRemove(key, out _);

        return Task.CompletedTask;
    }
}
