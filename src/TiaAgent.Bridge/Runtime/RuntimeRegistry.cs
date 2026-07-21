using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Registry of available IAgentRuntime instances.
/// Manages runtime selection with the specified precedence:
/// 1. Runtime explicitly included in the task request
/// 2. Environment variable (TIA_AGENT_RUNTIME)
/// 3. User configuration file (%LOCALAPPDATA%\TiaAgent\config.json)
/// 4. Configured default
///
/// No silent fallback: returns actionable errors when a runtime is unavailable.
/// </summary>
public sealed class RuntimeRegistry : IDisposable
{
    private readonly Dictionary<string, IAgentRuntime> _runtimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly TiaAgentConfig _config;
    private readonly BridgeLogger _logger;

    public RuntimeRegistry(TiaAgentConfig config, BridgeLogger logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Registers a runtime adapter. Called during Bridge startup.
    /// </summary>
    public void Register(IAgentRuntime runtime)
    {
        _runtimes[runtime.Id] = runtime;
        _logger.Info($"RuntimeRegistry: registered runtime '{runtime.Id}' ({runtime.DisplayName})");
    }

    /// <summary>
    /// Gets a runtime by ID. Throws with an actionable error if not found.
    /// </summary>
    public IAgentRuntime GetRuntime(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Runtime ID cannot be empty");

        if (_runtimes.TryGetValue(id, out var runtime))
            return runtime;

        var available = string.Join(", ", _runtimes.Keys.OrderBy(k => k));
        throw new InvalidOperationException(
            $"Unknown runtime '{id}'. Available runtimes: {available}");
    }

    /// <summary>
    /// Gets all registered runtimes.
    /// </summary>
    public IReadOnlyCollection<IAgentRuntime> GetAllRuntimes() => _runtimes.Values.ToList();

    /// <summary>
    /// Resolves which runtime to use for a task, implementing the precedence:
    /// 1. Request override
    /// 2. TIA_AGENT_RUNTIME environment variable
    /// 3. Config file default
    /// 4. Hardcoded default ("opencode")
    /// </summary>
    public IAgentRuntime ResolveRuntime(string? requestOverride)
    {
        // 1. Explicit request override
        if (!string.IsNullOrEmpty(requestOverride))
        {
            _logger.Info($"RuntimeRegistry: using request override '{requestOverride}'");
            return GetRuntime(requestOverride);
        }

        // 2. Environment variable
        var envRuntime = Environment.GetEnvironmentVariable("TIA_AGENT_RUNTIME");
        if (!string.IsNullOrEmpty(envRuntime))
        {
            _logger.Info($"RuntimeRegistry: using environment variable TIA_AGENT_RUNTIME='{envRuntime}'");
            return GetRuntime(envRuntime);
        }

        // 3. Config file default
        if (!string.IsNullOrEmpty(_config.DefaultRuntime))
        {
            _logger.Info($"RuntimeRegistry: using config default '{_config.DefaultRuntime}'");
            return GetRuntime(_config.DefaultRuntime);
        }

        // 4. Hardcoded default
        _logger.Info("RuntimeRegistry: using hardcoded default 'opencode'");
        return GetRuntime("opencode");
    }

    /// <summary>
    /// Checks availability of all registered runtimes.
    /// </summary>
    public async Task<Dictionary<string, RuntimeAvailabilityResult>> CheckAllAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, RuntimeAvailabilityResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in _runtimes)
        {
            try
            {
                results[kvp.Key] = await kvp.Value.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"RuntimeRegistry: availability check failed for '{kvp.Key}'", ex);
                results[kvp.Key] = new RuntimeAvailabilityResult
                {
                    Available = false,
                    Error = $"Availability check failed: {ex.Message}"
                };
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the configured default runtime ID.
    /// </summary>
    public string GetDefaultRuntimeId()
    {
        // Check env var first
        var envRuntime = Environment.GetEnvironmentVariable("TIA_AGENT_RUNTIME");
        if (!string.IsNullOrEmpty(envRuntime))
            return envRuntime;

        // Then config
        if (!string.IsNullOrEmpty(_config.DefaultRuntime))
            return _config.DefaultRuntime;

        // Hardcoded default
        return "opencode";
    }

    public void Dispose()
    {
        foreach (var runtime in _runtimes.Values)
        {
            if (runtime is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }
        }
        _runtimes.Clear();
    }
}
