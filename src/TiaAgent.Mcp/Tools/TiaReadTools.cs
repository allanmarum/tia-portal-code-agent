using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA block reading operations.
/// </summary>
[McpServerToolType]
public class TiaReadTools
{
    private readonly ITiaProjectService _projectService;

    public TiaReadTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    [McpServerTool(Name = "tia_list_blocks", ReadOnly = true)]
    [Description("Lists PLC blocks in the active TIA Portal project with optional filtering by type, language, or name. Returns paginated results.")]
    public Task<PagedResultDto<BlockSummaryDto>> ListBlocks(
        ListBlocksRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.ListBlocksAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "tia_read_block", ReadOnly = true)]
    [Description("Reads a PLC block's source code and interface. Accepts either an objectId or a selectionToken to identify the block.")]
    public Task<BlockSnapshotDto> ReadBlock(
        ReadBlockRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.ReadBlockAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "tia_get_block_interface", ReadOnly = true)]
    [Description("Returns the interface definition of a PLC block including input, output, in-out parameters, static variables, and temp variables.")]
    public Task<BlockInterfaceDto> GetBlockInterface(
        GetBlockInterfaceRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.GetBlockInterfaceAsync(request, cancellationToken);
    }
}
