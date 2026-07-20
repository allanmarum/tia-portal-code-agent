namespace TiaAgent.Contracts.Bridge;

public interface IAgentBridgeClient
{
    Task<BridgeHealthResponse> CheckHealthAsync(CancellationToken cancellationToken);
    Task<BridgeTaskAccepted> StartTaskAsync(BridgeTaskRequest request, CancellationToken cancellationToken);
    Task<BridgeTaskStatus> GetTaskAsync(string taskId, CancellationToken cancellationToken);
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken);
}
