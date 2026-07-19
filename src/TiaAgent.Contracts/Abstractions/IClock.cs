namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Testable system clock abstraction for deterministic time in tests.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
