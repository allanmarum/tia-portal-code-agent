using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Manages change sets for the controlled write workflow.
/// </summary>
public interface IChangeSetStore
{
    Task<ChangeSetDto?> GetAsync(string changeSetId, CancellationToken cancellationToken);
    Task<ChangeSetDto> CreateAsync(ChangeSetDto changeSet, CancellationToken cancellationToken);
    Task<ChangeSetDto> UpdateStatusAsync(string changeSetId, ChangeSetStatus status, CancellationToken cancellationToken);
    Task ExpireAsync(string changeSetId, string reason, CancellationToken cancellationToken);
}
