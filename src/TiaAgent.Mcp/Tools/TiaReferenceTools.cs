using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA reference and hierarchy operations.
/// </summary>
[McpServerToolType]
public class TiaReferenceTools
{
    private readonly ITiaProjectService _projectService;

    public TiaReferenceTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    [McpServerTool(Name = "tia_get_call_hierarchy", ReadOnly = true)]
    [Description("Returns the call hierarchy tree for a PLC block, showing which blocks it calls and their children.")]
    public Task<CallHierarchyDto> GetCallHierarchy(
        GetCallHierarchyRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.GetCallHierarchyAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "tia_find_references", ReadOnly = true)]
    [Description("Finds all references to a PLC block or symbol in the active TIA Portal project.")]
    public Task<ReferenceSearchResultDto> FindReferences(
        FindReferencesRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.FindReferencesAsync(request, cancellationToken);
    }
}
