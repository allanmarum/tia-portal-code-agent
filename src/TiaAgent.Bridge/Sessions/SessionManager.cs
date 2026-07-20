using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.OpenCode;

namespace TiaAgent.Bridge.Sessions;

public sealed class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly OpenCodeClient _openCodeClient;

    public SessionManager(OpenCodeClient openCodeClient)
    {
        _openCodeClient = openCodeClient;
    }

    public int ActiveSessionCount => _sessions.Count;

    public async Task<string> GetOrCreateSessionAsync(string projectKey, string agentId, string prompt, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(projectKey, out var existing))
            return existing.SessionId;

        var response = await _openCodeClient.CreateSessionAsync(agentId, prompt, cancellationToken).ConfigureAwait(false);
        if (!response.Success || string.IsNullOrEmpty(response.SessionId))
            throw new InvalidOperationException($"Failed to create session: {response.RawJson}");

        var entry = new SessionEntry
        {
            SessionId = response.SessionId,
            CreatedAt = DateTime.UtcNow
        };
        _sessions[projectKey] = entry;
        return entry.SessionId;
    }

    public bool RemoveSession(string projectKey)
    {
        return _sessions.TryRemove(projectKey, out _);
    }

    public bool TryGetSession(string projectKey, out string? sessionId)
    {
        if (_sessions.TryGetValue(projectKey, out var entry))
        {
            sessionId = entry.SessionId;
            return true;
        }
        sessionId = null;
        return false;
    }

    public void Dispose()
    {
        foreach (var kvp in _sessions)
        {
            try { _openCodeClient.AbortSessionAsync(kvp.Value.SessionId).GetAwaiter().GetResult(); } catch { }
        }
        _sessions.Clear();
    }

    private sealed class SessionEntry
    {
        public string SessionId { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
    }
}
