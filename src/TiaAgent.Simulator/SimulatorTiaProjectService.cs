using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Simulator;

/// <summary>
/// In-memory ITiaProjectService implementation with realistic sample data.
/// Enables full development and testing without TIA Portal.
/// </summary>
public class SimulatorTiaProjectService : ITiaProjectService, ISelectionTokenFactory
{
    private readonly SimulatorData _data;
    private readonly IContentHashService _hashService;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly SimulatorScenarioConfig _config;

    public SimulatorTiaProjectService(
        IContentHashService hashService,
        IClock clock,
        SimulatorScenarioConfig? config = null,
        IIdGenerator? idGenerator = null)
    {
        _hashService = hashService;
        _clock = clock;
        _config = config ?? new SimulatorScenarioConfig();
        _idGenerator = idGenerator ?? new TiaAgent.Application.Common.GuidIdGenerator();
        _data = new SimulatorData(hashService, clock);
    }

    public Task<TiaContextDto> GetCurrentContextAsync(CancellationToken cancellationToken)
    {
        if (_config.SimulateDisconnected)
            throw new TiaErrorException(TiaErrorCode.TIA_NOT_CONNECTED, "TIA Portal is not connected.");

        return Task.FromResult(_data.Context);
    }

    public Task<SelectionSnapshotDto> GetSelectionAsync(string selectionToken, CancellationToken cancellationToken)
    {
        if (_config.SimulateExpiredSelection)
            throw new TiaErrorException(TiaErrorCode.TIA_SELECTION_EXPIRED, $"Selection token '{selectionToken}' has expired.", selectionToken);

        if (!_data.Selections.TryGetValue(selectionToken, out var snapshot))
            throw new TiaErrorException(TiaErrorCode.TIA_SELECTION_EXPIRED, $"Selection token '{selectionToken}' not found.", selectionToken);

        return Task.FromResult(snapshot);
    }

    public Task<PagedResultDto<BlockSummaryDto>> ListBlocksAsync(ListBlocksRequest request, CancellationToken cancellationToken)
    {
        var blocks = _data.Blocks.AsEnumerable();

        if (!string.IsNullOrEmpty(request.TypeFilter))
            blocks = blocks.Where(b => b.BlockType.Equals(request.TypeFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(request.LanguageFilter))
            blocks = blocks.Where(b => b.Language.Equals(request.LanguageFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(request.NameFilter))
            blocks = blocks.Where(b => b.Name.IndexOf(request.NameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

        var allBlocks = blocks.ToList();
        var pageSize = Math.Max(1, Math.Min(request.PageSize, Contracts.Common.TiaLimits.MaxPageSize));

        int startIndex = 0;
        if (!string.IsNullOrEmpty(request.Cursor) && int.TryParse(request.Cursor, out var parsed))
            startIndex = parsed;

        var page = allBlocks.Skip(startIndex).Take(pageSize).ToList();
        var nextCursor = startIndex + pageSize < allBlocks.Count
            ? (startIndex + pageSize).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

        return Task.FromResult(new PagedResultDto<BlockSummaryDto>
        {
            Items = page,
            NextCursor = nextCursor,
            IsPartial = nextCursor != null,
            TotalCount = allBlocks.Count
        });
    }

    public Task<BlockSnapshotDto> ReadBlockAsync(ReadBlockRequest request, CancellationToken cancellationToken)
    {
        if (_config.SimulateObjectChanged && request.ObjectId == "block-conveyor")
            throw new TiaErrorException(TiaErrorCode.TIA_OBJECT_CHANGED, "Object has changed since the last snapshot.");

        var objectId = request.ObjectId;
        if (string.IsNullOrEmpty(objectId) && !string.IsNullOrEmpty(request.SelectionToken))
        {
            if (!_data.Selections.TryGetValue(request.SelectionToken!, out var sel))
                throw new TiaErrorException(TiaErrorCode.TIA_SELECTION_EXPIRED, "Selection token not found or expired.");
            objectId = sel.Objects.Count > 0 ? sel.Objects[0].ObjectId : null;
        }

        if (string.IsNullOrEmpty(objectId))
            throw new TiaErrorException(TiaErrorCode.INVALID_REQUEST, "Either ObjectId or SelectionToken must be provided.");

        if (!_data.BlockDetails.TryGetValue(objectId!, out var detail))
            throw new TiaErrorException(TiaErrorCode.TIA_OBJECT_NOT_FOUND, $"Block '{objectId}' not found.");

        var summary = _data.Blocks.First(b => b.ObjectId == objectId!);

        return Task.FromResult(new BlockSnapshotDto
        {
            ObjectId = objectId!,
            ProjectId = _data.Context.ProjectId,
            PlcId = summary.PlcId,
            Name = summary.Name,
            Path = summary.Path,
            BlockType = summary.BlockType,
            Language = summary.Language,
            SourceCode = request.IncludeSource ? detail.SourceCode : null,
            Interface = request.IncludeInterface ? detail.Interface : null,
            ContentHash = detail.ContentHash ?? "sha256:unknown",
            CapturedAt = _clock.UtcNow,
            Provenance = DataProvenance.Direct
        });
    }

    public Task<BlockInterfaceDto> GetBlockInterfaceAsync(GetBlockInterfaceRequest request, CancellationToken cancellationToken)
    {
        if (!_data.BlockDetails.TryGetValue(request.ObjectId, out var detail) || detail.Interface == null)
            throw new TiaErrorException(TiaErrorCode.TIA_OBJECT_NOT_FOUND, $"Block '{request.ObjectId}' not found or has no interface.");

        return Task.FromResult(detail.Interface);
    }

    public Task<CallHierarchyDto> GetCallHierarchyAsync(GetCallHierarchyRequest request, CancellationToken cancellationToken)
    {
        if (!_data.CallHierarchies.TryGetValue(request.ObjectId, out var hierarchy))
        {
            return Task.FromResult(new CallHierarchyDto
            {
                RootObjectId = request.ObjectId,
                RootName = _data.Blocks.FirstOrDefault(b => b.ObjectId == request.ObjectId)?.Name ?? "Unknown",
                Nodes = Array.Empty<CallHierarchyNodeDto>(),
                IsPartial = false
            });
        }

        var count = 0;
        var limitedNodes = TruncateHierarchy(hierarchy.Nodes, request.MaxDepth, request.MaxNodes, 0, ref count);
        return Task.FromResult(new CallHierarchyDto
        {
            RootObjectId = hierarchy.RootObjectId,
            RootName = hierarchy.RootName,
            Nodes = limitedNodes,
            IsPartial = count >= request.MaxNodes
        });
    }

    public Task<ReferenceSearchResultDto> FindReferencesAsync(FindReferencesRequest request, CancellationToken cancellationToken)
    {
        var results = _data.References.AsEnumerable();

        if (!string.IsNullOrEmpty(request.ObjectId))
            results = results.Where(r => r.TargetObjectId == request.ObjectId);
        if (!string.IsNullOrEmpty(request.SymbolName))
            results = results.Where(r => r.TargetName.Equals(request.SymbolName, StringComparison.OrdinalIgnoreCase));

        var limited = results.Take(Math.Min(request.MaxResults, 100)).ToList();

        return Task.FromResult(new ReferenceSearchResultDto
        {
            References = limited,
            IsPartial = limited.Count >= request.MaxResults,
            TotalCount = _data.References.Count(r =>
                (string.IsNullOrEmpty(request.ObjectId) || r.TargetObjectId == request.ObjectId) &&
                (string.IsNullOrEmpty(request.SymbolName) || r.TargetName.Equals(request.SymbolName, StringComparison.OrdinalIgnoreCase)))
        });
    }

    public Task<CompileResultDto> CompileSoftwareAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        if (_config.SimulateCompileFailure)
        {
            return Task.FromResult(new CompileResultDto
            {
                Success = false,
                Messages = new List<CompileMessageDto>
                {
                    new() { Severity = "Error", Code = "SE2030", Message = "Cannot resolve function block 'FB_Missing'.", ObjectPath = "PLC_1/Program blocks/OB_Main" },
                    new() { Severity = "Warning", Code = "SE3015", Message = "Variable '_unused' is declared but never used.", ObjectPath = "PLC_1/Program blocks/FB_Conveyor", Line = 12 }
                },
                Duration = TimeSpan.FromSeconds(2.3),
                CorrelationId = request.TargetId ?? "compile-001"
            });
        }

        return Task.FromResult(new CompileResultDto
        {
            Success = true,
            Messages = new List<CompileMessageDto>
            {
                new() { Severity = "Info", Message = "Compilation completed successfully.", ObjectPath = "PLC_1" }
            },
            Duration = TimeSpan.FromSeconds(1.8),
            CorrelationId = request.TargetId ?? "compile-001"
        });
    }

    public Task<ChangePreviewDto> PreviewBlockChangeAsync(PreviewBlockChangeRequest request, CancellationToken cancellationToken)
    {
        var changeSetId = _idGenerator.NewChangeSetId();
        var diffHash = _hashService.ComputeHash(request.ProposedSource);

        return Task.FromResult(new ChangePreviewDto
        {
            ChangeSetId = changeSetId,
            Diff = $"--- Original\n+++ Proposed\n@@ -1,10 +1,10 @@\n{request.ProposedSource}",
            Risks = new List<string> { "Logic change detected", "Review before applying" },
            DiffHash = diffHash,
            CreatedAt = _clock.UtcNow,
            ExpiresAt = _clock.UtcNow.AddMinutes(10),
            Status = ChangeSetStatus.AwaitingApproval
        });
    }

    public Task<ApplyChangeResultDto> ApplyApprovedBlockChangeAsync(ApplyApprovedBlockChangeRequest request, CancellationToken cancellationToken)
    {
        if (_config.SimulateApprovalExpired)
            throw new TiaErrorException(TiaErrorCode.APPROVAL_EXPIRED, "The approval token has expired.");

        if (_config.SimulateApprovalAlreadyUsed)
            throw new TiaErrorException(TiaErrorCode.APPROVAL_ALREADY_USED, "The approval token has already been used.");

        return Task.FromResult(new ApplyChangeResultDto
        {
            Success = true,
            ChangeSetId = request.ChangeSetId,
            CompileResult = new CompileResultDto
            {
                Success = true,
                Messages = new List<CompileMessageDto>
                {
                    new() { Severity = "Info", Message = "Change applied and compiled successfully." }
                },
                Duration = TimeSpan.FromSeconds(1.5),
                CorrelationId = request.ChangeSetId
            }
        });
    }

    public void CreateSelectionToken(string token, string objectId, string objectName, string objectType, string path)
    {
        var snapshot = new SelectionSnapshotDto
        {
            SelectionToken = token,
            TiaSessionId = _data.Context.TiaSessionId,
            ProjectId = _data.Context.ProjectId,
            ProjectName = _data.Context.ProjectName,
            CreatedAt = _clock.UtcNow,
            ExpiresAt = _clock.UtcNow.AddMinutes(30),
            Objects = new List<SelectedObjectDto>
            {
                new()
                {
                    ObjectId = objectId,
                    NameAtCapture = objectName,
                    PathAtCapture = path,
                    ObjectType = objectType,
                    PlcId = "plc-001",
                    PlcName = "PLC_1"
                }
            }
        };
        _data.Selections[token] = snapshot;
    }

    private static IReadOnlyList<CallHierarchyNodeDto> TruncateHierarchy(
        IReadOnlyList<CallHierarchyNodeDto> nodes, int maxDepth, int maxNodes, int currentDepth, ref int count)
    {
        if (currentDepth >= maxDepth || count >= maxNodes)
            return Array.Empty<CallHierarchyNodeDto>();

        var result = new List<CallHierarchyNodeDto>();
        foreach (var node in nodes)
        {
            if (count >= maxNodes) break;
            count++;
            result.Add(new CallHierarchyNodeDto
            {
                ObjectId = node.ObjectId,
                Name = node.Name,
                BlockType = node.BlockType,
                Path = node.Path,
                Children = TruncateHierarchy(node.Children, maxDepth, maxNodes, currentDepth + 1, ref count)
            });
        }
        return result;
    }
}

public class SimulatorScenarioConfig
{
    public bool SimulateDisconnected { get; set; }
    public bool SimulateExpiredSelection { get; set; }
    public bool SimulateObjectChanged { get; set; }
    public bool SimulateCompileFailure { get; set; }
    public bool SimulateApprovalExpired { get; set; }
    public bool SimulateApprovalAlreadyUsed { get; set; }
}
