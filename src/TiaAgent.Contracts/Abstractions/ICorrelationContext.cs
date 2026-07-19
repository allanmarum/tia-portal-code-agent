namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Provides the current correlation ID for request tracing.
/// </summary>
public interface ICorrelationContext
{
    string CurrentCorrelationId { get; }
    IDisposable SetCorrelationId(string correlationId);
}
