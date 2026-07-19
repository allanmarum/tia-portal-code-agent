using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Manages the OpenCode process lifecycle.
/// </summary>
public interface IOpenCodeProcessManager
{
    Task<bool> StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<bool> IsRunningAsync(CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}
