namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskStatus
{
    public string TaskId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Response { get; set; } = null!;
    public BridgeError? Error { get; set; }

    /// <summary>
    /// The ID of the runtime that executed (or is executing) this task.
    /// </summary>
    public string? RuntimeId { get; set; }

    /// <summary>
    /// The version of the runtime that executed this task.
    /// </summary>
    public string? RuntimeVersion { get; set; }
}
