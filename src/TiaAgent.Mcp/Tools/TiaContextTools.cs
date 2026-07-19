using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA context and selection operations.
/// Each handler is a thin adapter delegating to ITiaProjectService.
/// </summary>
public class TiaContextTools
{
    private readonly ITiaProjectService _projectService;

    public TiaContextTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<TiaContextDto> GetCurrentContext(CancellationToken cancellationToken)
    {
        return _projectService.GetCurrentContextAsync(cancellationToken);
    }

    public Task<SelectionSnapshotDto> GetCurrentSelection(string selectionToken, CancellationToken cancellationToken)
    {
        return _projectService.GetSelectionAsync(selectionToken, cancellationToken);
    }
}
