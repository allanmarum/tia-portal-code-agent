using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA controlled change operations.
/// </summary>
[McpServerToolType]
public class TiaChangeTools
{
    private readonly ITiaProjectService _projectService;

    public TiaChangeTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    [McpServerTool(Name = "tia_preview_block_change", ReadOnly = false)]
    [Description("Previews a proposed change to a PLC block, returning a diff, risk assessment, and change set ID for approval workflow.")]
    public Task<ChangePreviewDto> PreviewBlockChange(
        PreviewBlockChangeRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.PreviewBlockChangeAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "tia_apply_approved_block_change", ReadOnly = false)]
    [Description("Applies an approved block change using the provided change set ID and approval token. Requires prior preview and user approval.")]
    public Task<ApplyChangeResultDto> ApplyApprovedBlockChange(
        ApplyApprovedBlockChangeRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.ApplyApprovedBlockChangeAsync(request, cancellationToken);
    }
}
