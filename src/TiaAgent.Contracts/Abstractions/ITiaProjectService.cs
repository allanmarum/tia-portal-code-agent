using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// The single source of truth for all TIA Portal project access.
/// Only one implementation should exist in the system (Openness adapter or Simulator).
/// All MCP handlers, UI commands, and application services must delegate through this interface.
/// </summary>
public interface ITiaProjectService
{
    Task<TiaContextDto> GetCurrentContextAsync(CancellationToken cancellationToken);

    Task<SelectionSnapshotDto> GetSelectionAsync(string selectionToken, CancellationToken cancellationToken);

    Task<PagedResultDto<BlockSummaryDto>> ListBlocksAsync(ListBlocksRequest request, CancellationToken cancellationToken);

    Task<BlockSnapshotDto> ReadBlockAsync(ReadBlockRequest request, CancellationToken cancellationToken);

    Task<BlockInterfaceDto> GetBlockInterfaceAsync(GetBlockInterfaceRequest request, CancellationToken cancellationToken);

    Task<CallHierarchyDto> GetCallHierarchyAsync(GetCallHierarchyRequest request, CancellationToken cancellationToken);

    Task<ReferenceSearchResultDto> FindReferencesAsync(FindReferencesRequest request, CancellationToken cancellationToken);

    Task<CompileResultDto> CompileSoftwareAsync(CompileRequest request, CancellationToken cancellationToken);

    Task<ChangePreviewDto> PreviewBlockChangeAsync(PreviewBlockChangeRequest request, CancellationToken cancellationToken);

    Task<ApplyChangeResultDto> ApplyApprovedBlockChangeAsync(ApplyApprovedBlockChangeRequest request, CancellationToken cancellationToken);
}
