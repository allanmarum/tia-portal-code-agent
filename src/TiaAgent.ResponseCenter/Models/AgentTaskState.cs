namespace TiaAgent.ResponseCenter.Models;

/// <summary>
/// Explicit UI state model for the Agent Response Center.
/// Each state drives a distinct visual representation and available actions.
/// </summary>
public enum AgentTaskState
{
    /// <summary>Task context captured, not yet submitted.</summary>
    Created,

    /// <summary>Task is being submitted to the Bridge.</summary>
    Submitting,

    /// <summary>Task accepted by Bridge, waiting for a runtime to pick it up.</summary>
    Queued,

    /// <summary>Runtime is actively executing the task.</summary>
    Running,

    /// <summary>Runtime requires human approval before proceeding.</summary>
    WaitingForApproval,

    /// <summary>Task finished successfully; response is available.</summary>
    Completed,

    /// <summary>Task failed; error details are available.</summary>
    Failed,

    /// <summary>Task was cancelled by the user.</summary>
    Cancelled,

    /// <summary>Lost contact with the Bridge or runtime.</summary>
    Disconnected
}
