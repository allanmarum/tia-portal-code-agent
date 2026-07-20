#if SIEMENS
using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TiaAgent.AddIn.Diagnostics;
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

    static AddInServices()
    {
        AddInLogger.Startup();
        AddInLogger.Info("AddInServices static constructor called");
    }

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
        AddInLogger.Info("Creating OpenCode orchestrator");
        try
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
            ILogger<OpenCodeOrchestrator> logger = new AddInLoggerAdapter<OpenCodeOrchestrator>();

            AddInLogger.Info("OpenCode orchestrator created successfully");
            return new OpenCodeOrchestrator(client, idGenerator, clock, logger);
        }
        catch (Exception ex)
        {
            AddInLogger.Error("Failed to create OpenCode orchestrator", ex);
            throw;
        }
    }

    /// <summary>
    /// Logger adapter that routes to AddInLogger file output.
    /// </summary>
    private sealed class AddInLoggerAdapter<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            switch (logLevel)
            {
                case LogLevel.Error:
                case LogLevel.Critical:
                    AddInLogger.Error(message, exception);
                    break;
                case LogLevel.Warning:
                    AddInLogger.Warn(message);
                    break;
                default:
                    AddInLogger.Info(message);
                    break;
            }
        }
    }
}
#endif
