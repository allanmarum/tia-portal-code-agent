namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Compilation result with messages. Returned by tia_compile_software.
/// </summary>
public class CompileResultDto
{
    public bool Success { get; init; }
    public required IReadOnlyList<CompileMessageDto> Messages { get; init; }
    public TimeSpan Duration { get; init; }
    public required string CorrelationId { get; init; }
}

public class CompileMessageDto
{
    public required string Severity { get; init; }
    public string? Code { get; init; }
    public required string Message { get; init; }
    public string? ObjectPath { get; init; }
    public int? Line { get; init; }
}
