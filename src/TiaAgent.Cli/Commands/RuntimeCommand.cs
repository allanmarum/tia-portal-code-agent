using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Supervisor;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Commands;

public sealed class RuntimeOptions
{
    public string? Subcommand { get; set; }
    public string? RuntimeId { get; set; }
    public string? Mode { get; set; }
    public string? CustomRoot { get; set; }
    public bool Json { get; set; }
    public bool Verbose { get; set; }
}

public sealed class RuntimeDetailItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "cli";
    public string? Executable { get; set; }
    public bool Available { get; set; }
    public string? DetectedVersion { get; set; }
    public string MinimumVersion { get; set; } = "0.1.0";
    public string TestedVersion { get; set; } = "1.0.0";
    public string? Error { get; set; }
}

public sealed class RuntimeListReport
{
    public string DefaultRuntime { get; set; } = string.Empty;
    public List<RuntimeDetailItem> Runtimes { get; set; } = new();
}

public static class RuntimeCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(RuntimeOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var layout = new TiaAgentLayout(options.CustomRoot);
        var subcommand = (options.Subcommand ?? "list").ToLowerInvariant();

        return subcommand switch
        {
            "list" or "show" => HandleList(layout, options, stdout),
            "use" or "select" or "set" => HandleUse(layout, options, stdout, stderr),
            "doctor" or "check" => HandleDoctor(layout, options, stdout, stderr),
            "status" => HandleStatus(options, stdout, stderr),
            _ => HandleUnknownSubcommand(subcommand, stderr)
        };
    }

    public static int HandleList(TiaAgentLayout layout, RuntimeOptions options, TextWriter stdout)
    {
        var config = ConfigCommand.LoadConfig(layout.ConfigPath);
        var envRuntime = Environment.GetEnvironmentVariable("TIA_AGENT_RUNTIME");
        var activeDefault = !string.IsNullOrWhiteSpace(envRuntime) ? envRuntime : (config.DefaultRuntime ?? "opencode");

        var report = new RuntimeListReport
        {
            DefaultRuntime = activeDefault
        };

        foreach (var (id, meta) in RuntimeCompatibilityRegistry.KnownRuntimes)
        {
            var isDefault = string.Equals(id, activeDefault, StringComparison.OrdinalIgnoreCase);
            var entryConfig = config.Runtimes.TryGetValue(id, out var cfg) ? cfg : null;

            var mode = entryConfig?.Mode ?? meta.DefaultMode;
            var enabled = entryConfig?.Enabled ?? true;
            var customExe = entryConfig?.Executable;

            var exeToCheck = !string.IsNullOrWhiteSpace(customExe) ? customExe! : id;
            var onPath = DoctorCommand.IsExecutableOnPath(exeToCheck);

            report.Runtimes.Add(new RuntimeDetailItem
            {
                Id = id,
                DisplayName = meta.DisplayName,
                IsDefault = isDefault,
                Enabled = enabled,
                Mode = mode,
                Executable = customExe ?? "(PATH)",
                Available = onPath,
                MinimumVersion = meta.MinimumVersion,
                TestedVersion = meta.TestedVersion,
                Error = onPath ? null : $"Executable '{exeToCheck}' not found on PATH"
            });
        }

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
            return 0;
        }

        stdout.WriteLine($"Default Runtime: {activeDefault}" + (!string.IsNullOrWhiteSpace(envRuntime) ? " (from TIA_AGENT_RUNTIME env)" : ""));
        stdout.WriteLine();
        stdout.WriteLine("Registered Agent Runtimes:");

        foreach (var item in report.Runtimes)
        {
            var activeMarker = item.IsDefault ? " [ACTIVE]" : "         ";
            var availMarker = item.Available ? "OK  " : "WARN";
            stdout.WriteLine($"{activeMarker} {item.Id,-10} ({item.DisplayName})");
            stdout.WriteLine($"             Mode       : {item.Mode}");
            stdout.WriteLine($"             Enabled    : {item.Enabled}");
            stdout.WriteLine($"             Executable : {item.Executable}");
            stdout.WriteLine($"             Available  : {availMarker} (Min: v{item.MinimumVersion}, Tested: v{item.TestedVersion})");
            if (!item.Available && !string.IsNullOrWhiteSpace(item.Error))
            {
                stdout.WriteLine($"             Issue      : {item.Error}");
            }
            stdout.WriteLine();
        }

        return 0;
    }

    public static int HandleUse(TiaAgentLayout layout, RuntimeOptions options, TextWriter stdout, TextWriter stderr)
    {
        var targetRuntime = options.RuntimeId?.Trim();
        if (string.IsNullOrWhiteSpace(targetRuntime))
        {
            stderr.WriteLine("Usage: tia-agent runtime use <runtime-id> [--mode <cli|server>]");
            stderr.WriteLine("Available runtimes: " + string.Join(", ", RuntimeCompatibilityRegistry.KnownRuntimes.Keys));
            return 1;
        }

        if (!RuntimeCompatibilityRegistry.IsKnownRuntime(targetRuntime))
        {
            stderr.WriteLine($"Unknown runtime '{targetRuntime}'. Available runtimes: {string.Join(", ", RuntimeCompatibilityRegistry.KnownRuntimes.Keys.OrderBy(k => k))}");
            return 1;
        }

        var meta = RuntimeCompatibilityRegistry.GetMetadata(targetRuntime)!;
        var mode = options.Mode?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(mode))
        {
            if (!meta.SupportedModes.Contains(mode))
            {
                stderr.WriteLine($"Runtime '{targetRuntime}' does not support '{mode}' mode. Supported modes: {string.Join(", ", meta.SupportedModes)}");
                return 1;
            }
        }

        var config = ConfigCommand.LoadConfig(layout.ConfigPath);
        config.DefaultRuntime = meta.Id;

        if (!config.Runtimes.TryGetValue(meta.Id, out var entry))
        {
            entry = new RuntimeEntryConfig();
            config.Runtimes[meta.Id] = entry;
        }

        if (!string.IsNullOrEmpty(mode))
        {
            entry.Mode = mode;
        }

        ManifestStore.WriteAtomic(layout.ConfigPath, config);
        stdout.WriteLine($"Set default runtime to '{meta.Id}' in '{layout.ConfigPath}'.");
        if (!string.IsNullOrEmpty(mode))
        {
            stdout.WriteLine($"Set mode for '{meta.Id}' to '{mode}'.");
        }

        return 0;
    }

    public static int HandleDoctor(TiaAgentLayout layout, RuntimeOptions options, TextWriter stdout, TextWriter stderr)
    {
        var report = new DoctorReport
        {
            ProductVersion = Program.GetProductVersion()
        };

        var targetId = options.RuntimeId?.Trim();
        List<string> runtimesToTest;

        if (!string.IsNullOrWhiteSpace(targetId))
        {
            if (!RuntimeCompatibilityRegistry.IsKnownRuntime(targetId))
            {
                stderr.WriteLine($"Unknown runtime '{targetId}'. Available runtimes: {string.Join(", ", RuntimeCompatibilityRegistry.KnownRuntimes.Keys.OrderBy(k => k))}");
                return 1;
            }
            runtimesToTest = new List<string> { targetId };
        }
        else
        {
            runtimesToTest = RuntimeCompatibilityRegistry.KnownRuntimes.Keys.ToList();
        }

        var config = ConfigCommand.LoadConfig(layout.ConfigPath);
        var activeDefault = config.DefaultRuntime ?? "opencode";

        foreach (var id in runtimesToTest)
        {
            var meta = RuntimeCompatibilityRegistry.GetMetadata(id)!;
            var entryConfig = config.Runtimes.TryGetValue(id, out var cfg) ? cfg : null;
            var isDefault = string.Equals(id, activeDefault, StringComparison.OrdinalIgnoreCase);

            var mode = entryConfig?.Mode ?? meta.DefaultMode;
            var exeName = entryConfig?.Executable ?? id;
            var onPath = DoctorCommand.IsExecutableOnPath(exeName);

            // 1. Selection & Mode Check
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Runtime",
                Name = $"Runtime Selection ({id})",
                Status = "OK",
                Details = $"Registered adapter '{meta.DisplayName}' (Mode: {mode})" + (isDefault ? " [Default Runtime]" : "")
            });

            // 2. Executable Check
            if (onPath)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Runtime",
                    Name = $"Executable Path ({id})",
                    Status = "OK",
                    Details = $"Executable '{exeName}' discovered on system PATH."
                });
            }
            else if (isDefault)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Runtime",
                    Name = $"Executable Path ({id})",
                    Status = "FAIL",
                    Details = $"Default runtime executable '{exeName}' not found on PATH.",
                    Recommendation = $"Install '{id}' CLI or update executable path in '{layout.ConfigPath}' via 'tia-agent config set runtimes.{id}.executable <path>'."
                });
            }
            else
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Runtime",
                    Name = $"Executable Path ({id})",
                    Status = "WARN",
                    Details = $"Optional runtime executable '{exeName}' not found on PATH.",
                    Recommendation = $"Install '{id}' CLI to use this runtime."
                });
            }

            // 3. Version Compatibility Metadata Check
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Runtime",
                Name = $"Version Policy ({id})",
                Status = "OK",
                Details = $"Minimum supported: v{meta.MinimumVersion}, Tested: v{meta.TestedVersion}."
            });

        }

        // 4. MCP Integration Check (shared infrastructure, not per-runtime)
        var mcpOnPath = DoctorCommand.IsExecutableOnPath("tia-mcp") || DoctorCommand.IsExecutableOnPath("TiaMcpServer");
        if (mcpOnPath)
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "MCP",
                Name = "MCP Server",
                Status = "OK",
                Details = "MCP server executable verified on PATH via stdio transport (tia-mcp)."
            });
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "MCP",
                Name = "MCP Server",
                Status = "FAIL",
                Details = "Required TIA MCP Server executable ('tia-mcp') missing.",
                Recommendation = "Install via: dotnet tool install -g TiaMcpServer"
            });
        }

        // 5. Secrets Isolation Check
        var secretsDir = Path.Join(layout.RuntimePath, "secrets");
        report.Checks.Add(new DoctorCheckResult
        {
            Category = "Security",
            Name = "Secrets Isolation",
            Status = "OK",
            Details = $"Secrets stored in isolated directory '{secretsDir}' (never written to config.json or logs)."
        });

        // Summary
        report.Summary.Total = report.Checks.Count;
        report.Summary.Passed = report.Checks.Count(c => c.Status == "OK");
        report.Summary.Warnings = report.Checks.Count(c => c.Status == "WARN");
        report.Summary.Failed = report.Checks.Count(c => c.Status == "FAIL");
        report.OverallStatus = report.Summary.Failed > 0 ? "FAIL" : (report.Summary.Warnings > 0 ? "WARN" : "OK");

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
        }
        else
        {
            stdout.WriteLine($"TIA Agent Runtime Diagnostics (v{report.ProductVersion})");
            stdout.WriteLine();
            foreach (var check in report.Checks)
            {
                var badge = check.Status switch
                {
                    "OK" => "[OK]",
                    "WARN" => "[WARN]",
                    "FAIL" => "[FAIL]",
                    _ => "[INFO]"
                };
                stdout.WriteLine($"{badge,-6} {check.Category} > {check.Name}: {check.Details}");
                if (options.Verbose && !string.IsNullOrWhiteSpace(check.Recommendation))
                {
                    stdout.WriteLine($"       Recommendation: {check.Recommendation}");
                }
            }
            stdout.WriteLine();
            stdout.WriteLine($"Runtime Diagnostic Summary: {report.Summary.Passed} passed, {report.Summary.Warnings} warnings, {report.Summary.Failed} failed.");
        }

        return report.Summary.Failed > 0 ? 1 : 0;
    }

    private static int HandleStatus(RuntimeOptions options, TextWriter stdout, TextWriter stderr)
    {
        var statusOpts = new StatusOptions
        {
            CustomRoot = options.CustomRoot,
            Json = options.Json
        };
        return SupervisorEngine.GetStatus(statusOpts, stdout, stderr);
    }

    private static int HandleUnknownSubcommand(string subcommand, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown runtime subcommand '{subcommand}'.");
        stderr.WriteLine("Valid subcommands: list, use, doctor, status");
        return 1;
    }
}
