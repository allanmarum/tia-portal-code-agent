using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA controlled change operations.
/// </summary>
public class TiaChangeTools
{
    private readonly ITiaProjectService _projectService;

    public TiaChangeTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<ChangePreviewDto> PreviewBlockChange(
        PreviewBlockChangeRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.PreviewBlockChangeAsync(request, cancellationToken);
    }

    public Task<ApplyChangeResultDto> ApplyApprovedBlockChange(
        ApplyApprovedBlockChangeRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.ApplyApprovedBlockChangeAsync(request, cancellationToken);
    }
}
