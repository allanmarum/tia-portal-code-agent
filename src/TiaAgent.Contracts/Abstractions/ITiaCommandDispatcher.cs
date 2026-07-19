namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Dispatches Openness operations with queueing, cancellation, timeouts, and session validation.
/// </summary>
public interface ITiaCommandDispatcher
{
    Task<T> DispatchAsync<T>(string correlationId, Func<CancellationToken, Task<T>> command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    Task DispatchAsync(string correlationId, Func<CancellationToken, Task> command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    Task CancelAllAsync(string? reason = null, CancellationToken cancellationToken = default);
}
