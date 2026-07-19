using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA reference and hierarchy operations.
/// </summary>
public class TiaReferenceTools
{
    private readonly ITiaProjectService _projectService;

    public TiaReferenceTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<CallHierarchyDto> GetCallHierarchy(
        GetCallHierarchyRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.GetCallHierarchyAsync(request, cancellationToken);
    }

    public Task<ReferenceSearchResultDto> FindReferences(
        FindReferencesRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.FindReferencesAsync(request, cancellationToken);
    }
}
