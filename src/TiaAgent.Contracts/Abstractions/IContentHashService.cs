namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Computes deterministic content hashes for concurrency validation.
/// </summary>
public interface IContentHashService
{
    string ComputeHash(string content);
    bool ValidateHash(string content, string expectedHash);
    string HashPrefix { get; }
}
