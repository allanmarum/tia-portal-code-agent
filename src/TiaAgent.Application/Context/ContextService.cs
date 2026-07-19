using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Application.Context;

public interface IContextService
{
    Task<TiaContextDto> GetCurrentContextAsync(CancellationToken cancellationToken);
    Task<SelectionSnapshotDto> CaptureSelectionAsync(string? requestedToken, CancellationToken cancellationToken);
}

public class ContextService : IContextService
{
    private readonly ITiaProjectService _projectService;

    public ContextService(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<TiaContextDto> GetCurrentContextAsync(CancellationToken cancellationToken)
    {
        return _projectService.GetCurrentContextAsync(cancellationToken);
    }

    public Task<SelectionSnapshotDto> CaptureSelectionAsync(string? requestedToken, CancellationToken cancellationToken)
    {
        return _projectService.GetSelectionAsync(
            requestedToken ?? throw new ArgumentNullException(nameof(requestedToken)),
            cancellationToken);
    }
}
