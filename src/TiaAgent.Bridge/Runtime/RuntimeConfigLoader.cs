using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Loads and validates the TIA Agent runtime configuration from %LOCALAPPDATA%\TiaAgent\config.json.
/// </summary>
public sealed class RuntimeConfigLoader
{
    private readonly BridgeLogger _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public RuntimeConfigLoader(BridgeLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Known runtime IDs and their display names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownRuntimes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mimo"] = "Mimo CLI",
            ["opencode"] = "OpenCode",
            ["claude"] = "Claude Code CLI",
        };

    /// <summary>
    /// Loads the configuration file. Returns defaults if the file is missing or invalid.
    /// </summary>
    public TiaAgentConfig Load()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            _logger.Info($"Runtime config not found at {configPath}, using defaults");
            return new TiaAgentConfig();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.Warn("Runtime config file is empty, using defaults");
                return new TiaAgentConfig();
            }

            var config = JsonSerializer.Deserialize<TiaAgentConfig>(json, s_jsonOptions);
            if (config == null)
            {
                _logger.Warn("Runtime config deserialized to null, using defaults");
                return new TiaAgentConfig();
            }

            Validate(config);
            _logger.Info($"Runtime config loaded: defaultRuntime={config.DefaultRuntime}, runtimes=[{string.Join(", ", config.Runtimes.Keys)}]");
            return config;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load runtime config from {configPath}, using defaults", ex);
            return new TiaAgentConfig();
        }
    }

    /// <summary>
    /// Validates the configuration and logs warnings for issues.
    /// </summary>
    private void Validate(TiaAgentConfig config)
    {
        // Validate default runtime is a known ID
        if (!string.IsNullOrEmpty(config.DefaultRuntime) &&
            !KnownRuntimes.ContainsKey(config.DefaultRuntime))
        {
            _logger.Warn($"Default runtime '{config.DefaultRuntime}' is not a known runtime. Known: {string.Join(", ", KnownRuntimes.Keys)}");
        }

        // Validate each runtime entry
        foreach (var kvp in config.Runtimes)
        {
            if (!KnownRuntimes.ContainsKey(kvp.Key))
            {
                _logger.Warn($"Runtime config contains unknown runtime ID '{kvp.Key}'");
            }

            if (!string.IsNullOrEmpty(kvp.Value.Mode) &&
                kvp.Value.Mode != "server" && kvp.Value.Mode != "cli")
            {
                _logger.Warn($"Runtime '{kvp.Key}' has invalid mode '{kvp.Value.Mode}'. Valid: server, cli");
            }
        }
    }

    /// <summary>
    /// Gets the path to the runtime configuration file.
    /// </summary>
    public static string GetConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TiaAgent", "config.json");
    }
}
