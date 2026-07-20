using System;
using System.Threading;
using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.Application.Common;

/// <summary>
/// Provides the current correlation ID using AsyncLocal for thread-safe, async-aware propagation.
/// </summary>
public class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public string CurrentCorrelationId => _current.Value ?? "none";

    public IDisposable SetCorrelationId(string correlationId)
    {
        var previous = _current.Value;
        _current.Value = correlationId;
        return new CorrelationScope(previous);
    }

    private sealed class CorrelationScope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public CorrelationScope(string? previous) => _previous = previous;

        public void Dispose()
        {
            if (!_disposed)
            {
                _current.Value = _previous;
                _disposed = true;
            }
        }
    }
}
