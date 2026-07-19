namespace TiaAgent.Contracts.Dtos;

/// <summary>
/// Change preview with diff, risks, and change set metadata.
/// </summary>
public class ChangePreviewDto
{
    public required string ChangeSetId { get; init; }
    public required string Diff { get; init; }
    public required IReadOnlyList<string> Risks { get; init; }
    public required string DiffHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public required ChangeSetStatus Status { get; init; }
}

/// <summary>
/// Result of applying an approved change.
/// </summary>
public class ApplyChangeResultDto
{
    public bool Success { get; init; }
    public required string ChangeSetId { get; init; }
    public CompileResultDto? CompileResult { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public string? RollbackInfo { get; init; }
}

/// <summary>
/// Change set tracking the full lifecycle of a proposed modification.
/// </summary>
public class ChangeSetDto
{
    public required string ChangeSetId { get; init; }
    public required string CorrelationId { get; init; }
    public required string ProjectId { get; init; }
    public required IReadOnlyList<ChangeSetTargetDto> Targets { get; init; }
    public required string DiffHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public required ChangeSetStatus Status { get; init; }
}

public class ChangeSetTargetDto
{
    public required string ObjectId { get; init; }
    public required string ExpectedContentHash { get; init; }
}

public enum ChangeSetStatus
{
    Draft,
    Previewing,
    AwaitingApproval,
    Approved,
    Applying,
    Applied,
    Compiling,
    Completed,
    Rejected,
    Expired,
    Conflicted,
    Failed,
    RolledBack
}

/// <summary>
/// Approval token for controlled write operations. Single-use, time-bound.
/// </summary>
public class ApprovalTokenDto
{
    public required string Token { get; init; }
    public required string ChangeSetId { get; init; }
    public required string DiffHash { get; init; }
    public required string ApprovedBy { get; init; }
    public DateTimeOffset ApprovedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public required IReadOnlyList<string> Scope { get; init; }
    public bool Used { get; init; }
}
