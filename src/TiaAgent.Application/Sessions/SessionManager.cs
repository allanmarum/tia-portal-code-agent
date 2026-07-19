using System.Collections.Concurrent;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Application.Sessions;

public class SessionManager : ITiaSessionManager
{
    private readonly ConcurrentDictionary<string, SessionInfoDto> _sessions = new();
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionManager(IIdGenerator idGenerator, IClock clock)
    {
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public Task<SessionInfoDto?> GetCurrentSessionAsync(CancellationToken cancellationToken)
    {
        var session = _sessions.Values
            .OrderByDescending(s => s.LastActivity)
            .FirstOrDefault();
        return Task.FromResult<SessionInfoDto?>(session);
    }

    public Task InvalidateSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        _sessions.TryRemove(sessionId, out _);
        SessionChanged?.Invoke(this, new SessionChangedEventArgs
        {
            SessionId = sessionId,
            Reason = "Session invalidated"
        });
        return Task.CompletedTask;
    }

    public SessionInfoDto CreateSession(string tiaVersion, string projectId, string projectName)
    {
        var session = new SessionInfoDto
        {
            SessionId = _idGenerator.NewSessionId(),
            TiaVersion = tiaVersion,
            OpennessVersion = tiaVersion,
            ProjectId = projectId,
            ProjectName = projectName,
            StartedAt = _clock.UtcNow,
            LastActivity = _clock.UtcNow,
            State = "Active"
        };
        _sessions[session.SessionId] = session;
        return session;
    }
}
