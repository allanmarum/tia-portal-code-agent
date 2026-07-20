namespace TiaAgent.Contracts.Bridge;

public static class BridgeTaskStatusValues
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string WaitingForApproval = "waiting_for_approval";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
