#if SIEMENS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Common;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Openness;

/// <summary>
/// Real TIA Portal Openness implementation of ITiaProjectService.
/// Core methods: GetCurrentContext, ListBlocks, ReadBlock.
/// Remaining methods are stubs returning TIA_OPERATION_NOT_SUPPORTED.
/// </summary>
public sealed class TiaProjectService : ITiaProjectService, IDisposable
{
    private readonly TiaPortal _tiaPortal;
    private readonly Project _project;
    private readonly IContentHashService _hashService;
    private readonly IClock _clock;

    public TiaProjectService(
        TiaPortal tiaPortal,
        Project project,
        IContentHashService hashService,
        IClock clock)
    {
        _tiaPortal = tiaPortal ?? throw new ArgumentNullException(nameof(tiaPortal));
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Creates a TiaProjectService by attaching to a running TIA Portal instance.
    /// </summary>
    public static TiaProjectService AttachToRunningInstance(
        IContentHashService hashService,
        IClock clock)
    {
        var processes = TiaPortal.GetProcesses();
        if (processes.Count == 0)
            throw new TiaErrorException(TiaErrorCode.TIA_NOT_CONNECTED, "No running TIA Portal instance found.");

        var process = processes.First();
        var tiaPortal = process.Attach();

        var project = tiaPortal.Projects.FirstOrDefault()
            ?? throw new TiaErrorException(TiaErrorCode.TIA_PROJECT_NOT_OPEN, "No project is open in TIA Portal.");

        return new TiaProjectService(tiaPortal, project, hashService, clock);
    }

    public Task<TiaContextDto> GetCurrentContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var plcCount = 0;
            var blockCount = 0;

            foreach (var device in _project.Devices)
            {
                var plc = GetPlcSoftware(device);
                if (plc != null)
                {
                    plcCount++;
                    blockCount += plc.BlockGroup?.Blocks?.Count ?? 0;
                }
            }

            var context = new TiaContextDto
            {
                TiaVersion = "V21",
                OpennessVersion = "V21",
                ProjectId = _project.Name,
                ProjectName = _project.Name,
                PlcCount = plcCount,
                BlockCount = blockCount,
                TiaSessionId = $"tia-{_project.Name}-{_clock.UtcNow:yyyyMMdd}",
                LastModified = _clock.UtcNow,
                Capabilities = new CapabilityDto
                {
                    ReadBlockSource = true,
                    FindReferences = false,
                    CompileSoftware = true,
                    ImportBlock = true,
                    HardwareWrites = false
                }
            };

            return Task.FromResult(context);
        }
        catch (TiaErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TiaErrorException(TiaErrorCode.INTERNAL_ERROR, $"Failed to get TIA context: {ex.Message}");
        }
    }

    public Task<SelectionSnapshotDto> GetSelectionAsync(string selectionToken, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "GetSelectionAsync is not implemented in TiaProjectService. Use the Add-In selection token factory.");
    }

    public Task<PagedResultDto<BlockSummaryDto>> ListBlocksAsync(ListBlocksRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var allBlocks = new List<BlockSummaryDto>();

            foreach (var device in _project.Devices)
            {
                var plc = GetPlcSoftware(device);
                if (plc == null) continue;

                var plcId = plc.Name;
                var blocks = plc.BlockGroup?.Blocks;
                if (blocks == null) continue;

                foreach (var block in blocks)
                {
                    var summary = MapBlockToSummary(block, plcId);
                    if (summary == null) continue;

                    if (!string.IsNullOrEmpty(request.TypeFilter) &&
                        !summary.BlockType.Equals(request.TypeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(request.LanguageFilter) &&
                        !summary.Language.Equals(request.LanguageFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(request.NameFilter) &&
                        summary.Name.IndexOf(request.NameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    allBlocks.Add(summary);
                }
            }

            var pageSize = Math.Max(1, Math.Min(request.PageSize, TiaLimits.MaxPageSize));
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
        catch (TiaErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TiaErrorException(TiaErrorCode.INTERNAL_ERROR, $"Failed to list blocks: {ex.Message}");
        }
    }

    public Task<BlockSnapshotDto> ReadBlockAsync(ReadBlockRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var block = FindBlock(request.ObjectId);
            if (block == null)
                throw new TiaErrorException(TiaErrorCode.TIA_OBJECT_NOT_FOUND, $"Block '{request.ObjectId}' not found.");

            var plcId = GetPlcIdForBlock(block);
            var sourceCode = request.IncludeSource ? ExportBlockSource(block) : null;
            var contentHash = _hashService.ComputeHash(sourceCode ?? block.Name);

            return Task.FromResult(new BlockSnapshotDto
            {
                ObjectId = request.ObjectId ?? block.Name,
                ProjectId = _project.Name,
                PlcId = plcId,
                Name = block.Name,
                Path = $"{plcId}/Program blocks/{block.Name}",
                BlockType = GetBlockTypeName(block),
                Language = GetBlockLanguage(block),
                SourceCode = sourceCode,
                ContentHash = contentHash,
                CapturedAt = _clock.UtcNow,
                Provenance = DataProvenance.Direct
            });
        }
        catch (TiaErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TiaErrorException(TiaErrorCode.INTERNAL_ERROR, $"Failed to read block: {ex.Message}");
        }
    }

    public Task<BlockInterfaceDto> GetBlockInterfaceAsync(GetBlockInterfaceRequest request, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "GetBlockInterfaceAsync is not yet implemented.");
    }

    public Task<CallHierarchyDto> GetCallHierarchyAsync(GetCallHierarchyRequest request, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "GetCallHierarchyAsync is not yet implemented.");
    }

    public Task<ReferenceSearchResultDto> FindReferencesAsync(FindReferencesRequest request, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "FindReferencesAsync is not yet implemented.");
    }

    public Task<CompileResultDto> CompileSoftwareAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "CompileSoftwareAsync is not yet implemented.");
    }

    public Task<ChangePreviewDto> PreviewBlockChangeAsync(PreviewBlockChangeRequest request, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "PreviewBlockChangeAsync is not yet implemented.");
    }

    public Task<ApplyChangeResultDto> ApplyApprovedBlockChangeAsync(ApplyApprovedBlockChangeRequest request, CancellationToken cancellationToken)
    {
        throw new TiaErrorException(TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
            "ApplyApprovedBlockChangeAsync is not yet implemented.");
    }

    public void Dispose()
    {
        _tiaPortal?.Dispose();
    }

    // --- Private helpers ---

    private static PlcSoftware? GetPlcSoftware(Device device)
    {
        foreach (DeviceItem deviceItem in device.DeviceItems)
        {
            var container = deviceItem.GetService<SoftwareContainer>();
            if (container?.Software is PlcSoftware plc)
                return plc;
        }
        return null;
    }

    private PlcBlock? FindBlock(string? objectId)
    {
        if (string.IsNullOrEmpty(objectId))
            return null;

        foreach (var device in _project.Devices)
        {
            var plc = GetPlcSoftware(device);
            if (plc?.BlockGroup?.Blocks == null) continue;

            foreach (var block in plc.BlockGroup.Blocks)
            {
                if (block.Name.Equals(objectId, StringComparison.OrdinalIgnoreCase) ||
                    $"{plc.Name}/{block.Name}".Equals(objectId, StringComparison.OrdinalIgnoreCase))
                {
                    return block;
                }
            }
        }
        return null;
    }

    private static string GetPlcIdForBlock(PlcBlock block)
    {
        IEngineeringObject? parent = block;
        while (parent != null)
        {
            if (parent is PlcSoftware plc)
                return plc.Name;
            parent = parent.Parent;
        }
        return "unknown";
    }

    private BlockSummaryDto? MapBlockToSummary(PlcBlock block, string plcId)
    {
        try
        {
            return new BlockSummaryDto
            {
                ObjectId = $"{plcId}/{block.Name}",
                Name = block.Name,
                BlockType = GetBlockTypeName(block),
                Path = $"{plcId}/Program blocks/{block.Name}",
                PlcId = plcId,
                Language = GetBlockLanguage(block),
                ContentHash = _hashService.ComputeHash(block.Name),
                LastObservedAt = _clock.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetBlockTypeName(PlcBlock block)
    {
        return block.GetType().Name switch
        {
            "OrganizationBlock" => "OB",
            "FunctionBlock" => "FB",
            "Function" => "FC",
            "DataBlock" => "DB",
            "GlobalDB" => "DB",
            "InstanceDB" => "DB",
            "ArrayDB" => "DB",
            _ => block.GetType().Name
        };
    }

    private static string GetBlockLanguage(PlcBlock block)
    {
        try
        {
            var lang = block.GetAttribute("ProgrammingLanguage");
            return lang?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string? ExportBlockSource(PlcBlock block)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TiaAgent");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"{block.Name}.exp");

            var fileInfo = new FileInfo(tempFile);
            block.Export(fileInfo, ExportOptions.WithDefaults);
            var source = File.ReadAllText(tempFile);

            try { File.Delete(tempFile); } catch { }

            return source;
        }
        catch
        {
            return null;
        }
    }
}
#endif
