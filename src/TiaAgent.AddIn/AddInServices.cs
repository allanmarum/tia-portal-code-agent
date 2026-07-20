#if SIEMENS
using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TiaAgent.Application.Common;
using TiaAgent.Application.OpenCode;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.OpenCode.Client;

namespace TiaAgent.AddIn;

/// <summary>
/// Static service locator for the TIA Portal Add-In.
/// Initialized once when the Add-In loads; provides access to orchestrator and services
/// without requiring constructor injection (TIA Portal instantiates Add-In classes directly).
/// </summary>
public static class AddInServices
{
    private static IOpenCodeOrchestrator? _orchestrator;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the OpenCode orchestrator, initializing it lazily on first access.
    /// </summary>
    public static IOpenCodeOrchestrator Orchestrator
    {
        get
        {
            if (_orchestrator == null)
            {
                lock (_lock)
                {
                    _orchestrator ??= CreateOrchestrator();
                }
            }
            return _orchestrator;
        }
    }

    /// <summary>
    /// Allows tests or initialization code to override the orchestrator.
    /// </summary>
    public static void SetOrchestrator(IOpenCodeOrchestrator orchestrator)
    {
        lock (_lock)
        {
            _orchestrator = orchestrator;
        }
    }

    private static OpenCodeOrchestrator CreateOrchestrator()
    {
        var options = new OpenCodeOptions
        {
            BaseUrl = "http://127.0.0.1:43120",
            DefaultAgent = "tia-explain",
            RequestTimeoutSeconds = 30
        };

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds)
        };

        IOpenCodeClient client = new OpenCodeHttpClient(httpClient, options);
        IIdGenerator idGenerator = new GuidIdGenerator();
        IClock clock = new SystemClock();
        ILogger<OpenCodeOrchestrator> logger = new NoOpLogger<OpenCodeOrchestrator>();

        return new OpenCodeOrchestrator(client, idGenerator, clock, logger);
    }

    /// <summary>
    /// Minimal no-op logger for Add-In context where no logging framework is configured.
    /// </summary>
    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
#endif
