namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// TIA session information.
/// </summary>
public class SessionInfoDto
{
    public required string SessionId { get; init; }
    public required string TiaVersion { get; init; }
    public required string OpennessVersion { get; init; }
    public required string ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivity { get; init; }
    public required string State { get; init; }
}
