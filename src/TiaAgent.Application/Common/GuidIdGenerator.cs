using System;
using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.Application.Common;

/// <summary>
/// GUID-based ID generator for sessions, tokens, and objects.
/// </summary>
public class GuidIdGenerator : IIdGenerator
{
    public string NewId() => Guid.NewGuid().ToString("N");
    public string NewSessionId() => $"tia-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    public string NewToken() => $"tok-{Guid.NewGuid():N}";
    public string NewChangeSetId() => $"change-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
    public string NewApprovalToken() => $"approval-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
    public string NewSelectionToken() => $"sel-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
}
