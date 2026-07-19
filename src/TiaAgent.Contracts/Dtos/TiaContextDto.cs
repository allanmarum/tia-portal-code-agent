namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Current TIA Portal session context. Returned by tia_get_current_context.
/// </summary>
public class TiaContextDto
{
    public required string TiaVersion { get; init; }
    public required string OpennessVersion { get; init; }
    public required string TiaSessionId { get; init; }
    public required string ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public string? ProjectPath { get; init; }
    public int PlcCount { get; init; }
    public int BlockCount { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public required CapabilityDto Capabilities { get; init; }
}

public class CapabilityDto
{
    public bool CaptureSelection { get; init; }
    public bool ListBlocks { get; init; }
    public bool ReadBlockSource { get; init; }
    public bool ReadBlockInterface { get; init; }
    public bool FindReferences { get; init; }
    public bool CompileSoftware { get; init; }
    public bool PreviewBlockChange { get; init; }
    public bool ImportBlock { get; init; }
    public bool HardwareWrites { get; init; }
    public bool SafetyWrites { get; init; }
    public bool DownloadToPlc { get; init; }
}
