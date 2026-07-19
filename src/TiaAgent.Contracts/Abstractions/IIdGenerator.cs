namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Generates stable, unique IDs for sessions, tokens, and objects.
/// </summary>
public interface IIdGenerator
{
    string NewId();
    string NewSessionId();
    string NewToken();
    string NewChangeSetId();
    string NewApprovalToken();
    string NewSelectionToken();
}
