using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Stores and retrieves immutable selection snapshots.
/// </summary>
public interface ISelectionSnapshotStore
{
    Task<SelectionSnapshotDto?> GetAsync(string selectionToken, CancellationToken cancellationToken);
    Task SaveAsync(SelectionSnapshotDto snapshot, CancellationToken cancellationToken);
    Task ExpireAsync(string selectionToken, string reason, CancellationToken cancellationToken);
    Task ExpireAllForSessionAsync(string tiaSessionId, string reason, CancellationToken cancellationToken);
}
