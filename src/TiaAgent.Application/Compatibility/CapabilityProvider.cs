using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Application.Compatibility;

public class CapabilityProvider : ICapabilityProvider
{
    private readonly ITiaProjectService _projectService;

    public CapabilityProvider(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task<CapabilityDto> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var context = await _projectService.GetCurrentContextAsync(cancellationToken);
        return context.Capabilities;
    }

    public async Task<bool> IsSupportedAsync(string capability, CancellationToken cancellationToken)
    {
        var capabilities = await GetCapabilitiesAsync(cancellationToken);
        return capability switch
        {
            nameof(CapabilityDto.CaptureSelection) => capabilities.CaptureSelection,
            nameof(CapabilityDto.ListBlocks) => capabilities.ListBlocks,
            nameof(CapabilityDto.ReadBlockSource) => capabilities.ReadBlockSource,
            nameof(CapabilityDto.ReadBlockInterface) => capabilities.ReadBlockInterface,
            nameof(CapabilityDto.FindReferences) => capabilities.FindReferences,
            nameof(CapabilityDto.CompileSoftware) => capabilities.CompileSoftware,
            nameof(CapabilityDto.PreviewBlockChange) => capabilities.PreviewBlockChange,
            nameof(CapabilityDto.ImportBlock) => capabilities.ImportBlock,
            nameof(CapabilityDto.HardwareWrites) => capabilities.HardwareWrites,
            nameof(CapabilityDto.SafetyWrites) => capabilities.SafetyWrites,
            nameof(CapabilityDto.DownloadToPlc) => capabilities.DownloadToPlc,
            _ => false
        };
    }
}
