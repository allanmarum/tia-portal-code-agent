using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.Application.Common;

/// <summary>
/// Testable system clock abstraction.
/// </summary>
public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
