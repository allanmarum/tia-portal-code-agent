using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Creates and validates approval tokens for controlled write operations.
/// </summary>
public interface IApprovalService
{
    Task<ApprovalTokenDto> CreateTokenAsync(ApprovalTokenRequest request, CancellationToken cancellationToken);
    Task<ApprovalValidationResult> ValidateTokenAsync(string approvalToken, string changeSetId, CancellationToken cancellationToken);
    Task ConsumeTokenAsync(string approvalToken, CancellationToken cancellationToken);
}

public class ApprovalTokenRequest
{
    public required string ChangeSetId { get; init; }
    public required string DiffHash { get; init; }
    public required string ApprovedBy { get; init; }
    public required IReadOnlyList<string> Scope { get; init; }
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(5);
}

public class ApprovalValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public ApprovalTokenDto? Token { get; init; }
}
