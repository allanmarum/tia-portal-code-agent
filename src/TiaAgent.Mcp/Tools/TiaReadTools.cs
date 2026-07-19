using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA block reading operations.
/// </summary>
public class TiaReadTools
{
    private readonly ITiaProjectService _projectService;

    public TiaReadTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<PagedResultDto<BlockSummaryDto>> ListBlocks(
        ListBlocksRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.ListBlocksAsync(request, cancellationToken);
    }

    public Task<BlockSnapshotDto> ReadBlock(
        ReadBlockRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.ReadBlockAsync(request, cancellationToken);
    }

    public Task<BlockInterfaceDto> GetBlockInterface(
        GetBlockInterfaceRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.GetBlockInterfaceAsync(request, cancellationToken);
    }
}
