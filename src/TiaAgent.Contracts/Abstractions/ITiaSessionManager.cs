using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Manages TIA Portal session lifecycle.
/// </summary>
public interface ITiaSessionManager
{
    Task<SessionInfoDto?> GetCurrentSessionAsync(CancellationToken cancellationToken);
    Task InvalidateSessionAsync(string sessionId, CancellationToken cancellationToken);
    event EventHandler<SessionChangedEventArgs>? SessionChanged;
}

public class SessionChangedEventArgs : EventArgs
{
    public required string SessionId { get; init; }
    public required string Reason { get; init; }
}
