namespace TiaAgent.ResponseCenter;

/// <summary>
/// Centralized UI strings for the Agent Response Center.
/// </summary>
public static class Strings
{
    // Window
    public const string WindowTitle = "AI Code Agent";

    // Status labels
    public const string StatusCreated = "Ready";
    public const string StatusSubmitting = "Submitting task…";
    public const string StatusQueued = "Queued — waiting for runtime…";
    public const string StatusRunning = "Running…";
    public const string StatusWaitingForApproval = "Waiting for approval";
    public const string StatusCompleted = "Completed";
    public const string StatusFailed = "Failed";
    public const string StatusCancelled = "Cancelled";
    public const string StatusDisconnected = "Disconnected";

    // Progress messages
    public const string ProgressSubmitting = "Sending task to the AI agent…";
    public const string ProgressQueued = "The task is queued and will start shortly.";
    public const string ProgressDefault = "Processing your request…";

    // Buttons
    public const string ButtonCancel = "Cancel";
    public const string ButtonRetry = "Retry";
    public const string ButtonCopy = "Copy";
    public const string ButtonCopyAll = "Copy All";
    public const string ButtonClose = "Close";
    public const string ButtonApprove = "Approve";
    public const string ButtonReject = "Reject";

    // Labels
    public const string LabelAction = "Action:";
    public const string LabelObject = "Object:";
    public const string LabelType = "Type:";
    public const string LabelPlc = "PLC:";
    public const string LabelProject = "Project:";
    public const string LabelStatus = "Status:";
    public const string LabelCorrelationId = "Correlation:";

    // Error messages
    public const string ErrorBridgeUnavailable = "Cannot reach the AI agent bridge. Please ensure the bridge service is running.";
    public const string ErrorTaskTimeout = "The task timed out. The AI agent may still be working — you can try again.";
    public const string ErrorPollingFailed = "Lost contact with the task. Retrying…";
    public const string ErrorGeneric = "An unexpected error occurred.";
    public const string ErrorCopyFailed = "Failed to copy to clipboard.";

    // Approval
    public const string ApprovalTitle = "Approval Required";
    public const string ApprovalDescription = "The AI agent is proposing changes that require your review before being applied.";

    // Copy
    public const string CopySuccess = "Copied to clipboard.";
}
