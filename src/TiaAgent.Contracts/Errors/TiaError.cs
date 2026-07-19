using System.Text.Json.Serialization;

namespace TiaAgent.Contracts.Errors;

/// <summary>
/// Structured error returned by all TIA Agent operations.
/// Never expose raw stack traces through MCP or IPC.
/// </summary>
public class TiaError
{
    public required TiaErrorCode Code { get; init; }
    public required string Message { get; init; }
    public bool Retryable { get; init; }
    public string? CorrelationId { get; init; }
    public Dictionary<string, object>? Details { get; init; }
    public string? Remediation { get; init; }

    [JsonIgnore]
    public Exception? InternalException { get; init; }

    public static TiaError NotFound(string message, string? correlationId = null) => new()
    {
        Code = TiaErrorCode.TIA_OBJECT_NOT_FOUND,
        Message = message,
        CorrelationId = correlationId
    };

    public static TiaError SessionExpired(string correlationId) => new()
    {
        Code = TiaErrorCode.TIA_SESSION_EXPIRED,
        Message = "TIA Portal session has expired. Please reconnect.",
        CorrelationId = correlationId
    };

    public static TiaError SelectionExpired(string correlationId) => new()
    {
        Code = TiaErrorCode.TIA_SELECTION_EXPIRED,
        Message = "The selection token has expired. Please re-select the object.",
        CorrelationId = correlationId
    };

    public static TiaError ObjectChanged(string objectId, string expectedHash, string actualHash, string correlationId) => new()
    {
        Code = TiaErrorCode.TIA_OBJECT_CHANGED,
        Message = $"Object '{objectId}' has changed since the last snapshot.",
        Retryable = false,
        CorrelationId = correlationId,
        Details = new Dictionary<string, object>
        {
            ["objectId"] = objectId,
            ["expectedHash"] = expectedHash,
            ["actualHash"] = actualHash
        }
    };

    public static TiaError NotSupported(string operation, string? correlationId = null) => new()
    {
        Code = TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED,
        Message = $"Operation '{operation}' is not supported in the current TIA Portal version.",
        CorrelationId = correlationId
    };

    public static TiaError ApprovalRequired(string changeSetId, string correlationId) => new()
    {
        Code = TiaErrorCode.APPROVAL_REQUIRED,
        Message = "This operation requires explicit user approval via the UI.",
        CorrelationId = correlationId,
        Details = new Dictionary<string, object> { ["changeSetId"] = changeSetId }
    };

    public static TiaError ApprovalExpired(string approvalToken, string correlationId) => new()
    {
        Code = TiaErrorCode.APPROVAL_EXPIRED,
        Message = "The approval token has expired. Please re-approve the change.",
        CorrelationId = correlationId
    };

    public static TiaError ApprovalAlreadyUsed(string approvalToken, string correlationId) => new()
    {
        Code = TiaErrorCode.APPROVAL_ALREADY_USED,
        Message = "The approval token has already been used. Each token is single-use.",
        CorrelationId = correlationId
    };

    public static TiaError Internal(string message, Exception? exception = null, string? correlationId = null) => new()
    {
        Code = TiaErrorCode.INTERNAL_ERROR,
        Message = message,
        Retryable = false,
        CorrelationId = correlationId,
        InternalException = exception
    };
}
