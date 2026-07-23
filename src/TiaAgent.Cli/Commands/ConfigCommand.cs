using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiaAgent.Cli.Layout;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Commands;

public sealed class ConfigOptions
{
    public string? Subcommand { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string? CustomRoot { get; set; }
    public bool Json { get; set; }
}

public static class ConfigCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(ConfigOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var layout = new TiaAgentLayout(options.CustomRoot);
        var action = (options.Subcommand ?? "list").ToLowerInvariant();

        return action switch
        {
            "list" or "show" => HandleList(layout, options, stdout),
            "get" => HandleGet(layout, options, stdout, stderr),
            "set" => HandleSet(layout, options, stdout, stderr),
            "path" => HandlePath(layout, options, stdout),
            "reset" => HandleReset(layout, stdout),
            _ => HandleUnknownSubcommand(action, stderr)
        };
    }

    private static int HandleList(TiaAgentLayout layout, ConfigOptions options, TextWriter stdout)
    {
        var config = LoadConfig(layout.ConfigPath);

        if (options.Json)
        {
            var json = JsonSerializer.Serialize(config, s_jsonOptions);
            stdout.WriteLine(json);
            return 0;
        }

        stdout.WriteLine($"Configuration File: {layout.ConfigPath}");
        stdout.WriteLine($"Default Runtime:    {config.DefaultRuntime}");
        stdout.WriteLine();

        if (config.Runtimes.Count == 0)
        {
            stdout.WriteLine("No per-runtime custom configurations defined.");
        }
        else
        {
            stdout.WriteLine("Runtime Configurations:");
            foreach (var (key, entry) in config.Runtimes)
            {
                stdout.WriteLine($"  [{key}]");
                stdout.WriteLine($"    Enabled:    {entry.Enabled}");
                stdout.WriteLine($"    Mode:       {entry.Mode ?? "(default)"}");
                stdout.WriteLine($"    Executable: {entry.Executable ?? "(PATH)"}");
                if (!string.IsNullOrWhiteSpace(entry.ServerUrl))
                {
                    stdout.WriteLine($"    Server URL: {entry.ServerUrl}");
                }
            }
        }

        return 0;
    }

    private static int HandleGet(TiaAgentLayout layout, ConfigOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            return HandleList(layout, options, stdout);
        }

        var config = LoadConfig(layout.ConfigPath);
        var key = options.Key.Trim();

        if (string.Equals(key, "defaultRuntime", StringComparison.OrdinalIgnoreCase))
        {
            stdout.WriteLine(config.DefaultRuntime);
            return 0;
        }

        if (key.StartsWith("runtimes.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var runtimeId = parts[1];
                if (config.Runtimes.TryGetValue(runtimeId, out var entry))
                {
                    var prop = parts.Length > 2 ? parts[2].ToLowerInvariant() : "all";
                    switch (prop)
                    {
                        case "enabled":
                            stdout.WriteLine(entry.Enabled.ToString().ToLowerInvariant());
                            return 0;
                        case "executable":
                            stdout.WriteLine(entry.Executable ?? "");
                            return 0;
                        case "mode":
                            stdout.WriteLine(entry.Mode ?? "");
                            return 0;
                        case "serverurl":
                            stdout.WriteLine(entry.ServerUrl ?? "");
                            return 0;
                        case "all":
                            var json = JsonSerializer.Serialize(entry, s_jsonOptions);
                            stdout.WriteLine(json);
                            return 0;
                    }
                }
                else
                {
                    stderr.WriteLine($"Runtime entry '{runtimeId}' not found in configuration.");
                    return 1;
                }
            }
        }

        stderr.WriteLine($"Unknown configuration key '{options.Key}'.");
        return 1;
    }

    private static int HandleSet(TiaAgentLayout layout, ConfigOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (string.IsNullOrWhiteSpace(options.Key) || options.Value is null)
        {
            stderr.WriteLine("Usage: tia-agent config set <key> <value>");
            return 1;
        }

        var config = LoadConfig(layout.ConfigPath);
        var key = options.Key.Trim();
        var val = options.Value;

        if (string.Equals(key, "defaultRuntime", StringComparison.OrdinalIgnoreCase))
        {
            config.DefaultRuntime = val;
        }
        else if (key.StartsWith("runtimes.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                stderr.WriteLine("Invalid runtime config key format. Expected 'runtimes.<runtimeId>.<property>'");
                return 1;
            }

            var runtimeId = parts[1];
            var propName = parts[2].ToLowerInvariant();

            if (!config.Runtimes.TryGetValue(runtimeId, out var entry))
            {
                entry = new RuntimeEntryConfig();
                config.Runtimes[runtimeId] = entry;
            }

            switch (propName)
            {
                case "enabled":
                    if (bool.TryParse(val, out var enabled))
                    {
                        entry.Enabled = enabled;
                    }
                    else
                    {
                        stderr.WriteLine($"Invalid boolean value '{val}' for enabled property.");
                        return 1;
                    }
                    break;
                case "executable":
                    entry.Executable = string.IsNullOrWhiteSpace(val) ? null : val;
                    break;
                case "mode":
                    entry.Mode = string.IsNullOrWhiteSpace(val) ? null : val;
                    break;
                case "serverurl":
                    entry.ServerUrl = string.IsNullOrWhiteSpace(val) ? null : val;
                    break;
                default:
                    stderr.WriteLine($"Unknown property '{parts[2]}' for runtime '{runtimeId}'.");
                    return 1;
            }
        }
        else
        {
            stderr.WriteLine($"Unknown configuration key '{options.Key}'.");
            return 1;
        }

        ManifestStore.WriteAtomic(layout.ConfigPath, config);
        stdout.WriteLine($"Set '{key}' to '{val}' in '{layout.ConfigPath}'.");
        return 0;
    }

    private static int HandlePath(TiaAgentLayout layout, ConfigOptions options, TextWriter stdout)
    {
        stdout.WriteLine(layout.ConfigPath);
        return 0;
    }

    private static int HandleReset(TiaAgentLayout layout, TextWriter stdout)
    {
        var defaultConfig = new TiaAgentConfig
        {
            DefaultRuntime = "opencode"
        };

        ManifestStore.WriteAtomic(layout.ConfigPath, defaultConfig);
        stdout.WriteLine($"Reset configuration to defaults at '{layout.ConfigPath}'.");
        return 0;
    }

    private static int HandleUnknownSubcommand(string subcommand, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown config subcommand '{subcommand}'.");
        stderr.WriteLine("Valid subcommands: list, get, set, path, reset");
        return 1;
    }

    public static TiaAgentConfig LoadConfig(string configPath)
    {
        if (File.Exists(configPath))
        {
            try
            {
                return ManifestStore.Read<TiaAgentConfig>(configPath);
            }
            catch
            {
                // Return default on corruption
            }
        }

        return new TiaAgentConfig();
    }
}
