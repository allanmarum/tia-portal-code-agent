using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using TiaAgent.Cli.Layout;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Commands;

public sealed class DoctorOptions
{
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
    public bool Json { get; set; }
    public bool Verbose { get; set; }
}

public sealed class DoctorCheckResult
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "OK"; // OK, WARN, FAIL
    public string Details { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
}

public sealed class DoctorReport
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string ProductVersion { get; set; } = string.Empty;
    public string OverallStatus { get; set; } = "OK"; // OK, WARN, FAIL
    public List<DoctorCheckResult> Checks { get; set; } = new();
    public DoctorSummary Summary { get; set; } = new();
}

public sealed class DoctorSummary
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Warnings { get; set; }
    public int Failed { get; set; }
}

public static class DoctorCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(DoctorOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var layout = new TiaAgentLayout(options.CustomRoot);
        var productVersion = Program.GetProductVersion();
        var report = new DoctorReport
        {
            ProductVersion = productVersion
        };

        // 1. Platform & Environment Check
        CheckEnvironment(report);

        // 2. Layout & Manifests Check
        CheckLayoutAndManifests(layout, report);

        // 3. Siemens TIA Portal V21 & Add-In Check
        CheckSiemensIntegration(options.UserAddInsDir, report);

        // 4. Runtime & MCP Server Availability Check
        CheckRuntimesAndMcp(layout, report);

        // Calculate summary
        report.Summary.Total = report.Checks.Count;
        report.Summary.Passed = report.Checks.Count(c => c.Status == "OK");
        report.Summary.Warnings = report.Checks.Count(c => c.Status == "WARN");
        report.Summary.Failed = report.Checks.Count(c => c.Status == "FAIL");

        if (report.Summary.Failed > 0)
        {
            report.OverallStatus = "FAIL";
        }
        else if (report.Summary.Warnings > 0)
        {
            report.OverallStatus = "WARN";
        }
        else
        {
            report.OverallStatus = "OK";
        }

        if (options.Json)
        {
            var json = JsonSerializer.Serialize(report, s_jsonOptions);
            stdout.WriteLine(json);
        }
        else
        {
            PrintConsoleReport(report, layout, stdout, options.Verbose);
        }

        return report.Summary.Failed > 0 ? 1 : 0;
    }

    private static void CheckEnvironment(DoctorReport report)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isX64 = RuntimeInformation.OSArchitecture == Architecture.X64;
        var osDesc = RuntimeInformation.OSDescription;

        if (isWindows && isX64)
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Platform",
                Name = "Operating System",
                Status = "OK",
                Details = $"Supported platform: {osDesc} ({RuntimeInformation.OSArchitecture})"
            });
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Platform",
                Name = "Operating System",
                Status = "WARN",
                Details = $"Current platform '{osDesc}' ({RuntimeInformation.OSArchitecture}) is not Windows x64.",
                Recommendation = "TIA Portal V21 Openness integration requires Windows x64."
            });
        }
    }

    private static void CheckLayoutAndManifests(TiaAgentLayout layout, DoctorReport report)
    {
        // Root path
        if (Directory.Exists(layout.RootPath))
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Layout",
                Name = "Filesystem Root",
                Status = "OK",
                Details = $"Root directory exists at '{layout.RootPath}'"
            });
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Layout",
                Name = "Filesystem Root",
                Status = "WARN",
                Details = $"Root directory does not exist at '{layout.RootPath}'",
                Recommendation = "Run 'tia-agent install' to initialize layout."
            });
        }

        // Config file
        if (File.Exists(layout.ConfigPath))
        {
            try
            {
                var config = ManifestStore.Read<TiaAgentConfig>(layout.ConfigPath);
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Config",
                    Name = "Configuration File",
                    Status = "OK",
                    Details = $"Valid config.json found at '{layout.ConfigPath}' (Default runtime: {config.DefaultRuntime})"
                });
            }
            catch (Exception ex)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Config",
                    Name = "Configuration File",
                    Status = "FAIL",
                    Details = $"Malformed config.json at '{layout.ConfigPath}': {ex.Message}",
                    Recommendation = "Run 'tia-agent config reset' or fix JSON formatting."
                });
            }
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Config",
                Name = "Configuration File",
                Status = "WARN",
                Details = $"Config file missing at '{layout.ConfigPath}'",
                Recommendation = "Run 'tia-agent install' or create config.json."
            });
        }

        // Active Version (current.json)
        if (File.Exists(layout.CurrentManifestPath))
        {
            try
            {
                var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
                var activeVersion = current.ActiveVersion;

                if (string.IsNullOrWhiteSpace(activeVersion))
                {
                    report.Checks.Add(new DoctorCheckResult
                    {
                        Category = "Installation",
                        Name = "Active Version",
                        Status = "WARN",
                        Details = "current.json present but activeVersion is empty.",
                        Recommendation = "Run 'tia-agent install' to select an active version."
                    });
                }
                else
                {
                    var versionPath = layout.GetVersionPath(activeVersion);
                    if (Directory.Exists(versionPath))
                    {
                        report.Checks.Add(new DoctorCheckResult
                        {
                            Category = "Installation",
                            Name = "Active Version",
                            Status = "OK",
                            Details = $"Active version 'v{activeVersion}' installed at '{versionPath}'"
                        });
                    }
                    else
                    {
                        report.Checks.Add(new DoctorCheckResult
                        {
                            Category = "Installation",
                            Name = "Active Version",
                            Status = "FAIL",
                            Details = $"Active version set to 'v{activeVersion}' but folder '{versionPath}' is missing!",
                            Recommendation = "Run 'tia-agent install --force' to repair installation."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Installation",
                    Name = "Active Version",
                    Status = "FAIL",
                    Details = $"Malformed current.json at '{layout.CurrentManifestPath}': {ex.Message}",
                    Recommendation = "Run 'tia-agent install --force' to re-activate a version."
                });
            }
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Installation",
                Name = "Active Version",
                Status = "WARN",
                Details = "No active version set ('current.json' missing).",
                Recommendation = "Run 'tia-agent install' to activate a version."
            });
        }

        // Installations registry
        if (File.Exists(layout.InstallationsManifestPath))
        {
            try
            {
                var installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Installation",
                    Name = "Installations Registry",
                    Status = "OK",
                    Details = $"Found {installations.Versions.Count} registered version(s) in installations.json"
                });
            }
            catch (Exception ex)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Installation",
                    Name = "Installations Registry",
                    Status = "WARN",
                    Details = $"Malformed installations.json: {ex.Message}"
                });
            }
        }
    }

    private static void CheckSiemensIntegration(string? customUserAddInsDir, DoctorReport report)
    {
        var tiaPublicApiDir = Environment.GetEnvironmentVariable("TiaPublicApiDir");
        var defaultTiaPath = @"C:\Program Files\Siemens\Automation\Portal V21";

        if (!string.IsNullOrWhiteSpace(tiaPublicApiDir) && Directory.Exists(tiaPublicApiDir))
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Siemens",
                Name = "TIA Portal V21 API",
                Status = "OK",
                Details = $"TIA Portal Public API directory set via environment: '{tiaPublicApiDir}'"
            });
        }
        else if (Directory.Exists(defaultTiaPath))
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Siemens",
                Name = "TIA Portal V21 API",
                Status = "OK",
                Details = $"TIA Portal V21 found at default location: '{defaultTiaPath}'"
            });
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Siemens",
                Name = "TIA Portal V21 API",
                Status = "WARN",
                Details = $"TIA Portal V21 not found at '{defaultTiaPath}' and TiaPublicApiDir env var not set.",
                Recommendation = "Install TIA Portal V21 on Windows or set TiaPublicApiDir."
            });
        }

        var userAddInsDir = customUserAddInsDir;
        if (string.IsNullOrWhiteSpace(userAddInsDir))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            userAddInsDir = Path.Combine(appData, "Siemens", "Automation", "Portal V21", "UserAddIns");
        }

        if (Directory.Exists(userAddInsDir))
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Siemens",
                Name = "UserAddIns Directory",
                Status = "OK",
                Details = $"UserAddIns directory exists at '{userAddInsDir}'"
            });

            var addinFiles = Directory.GetFiles(userAddInsDir, "*.addin");
            if (addinFiles.Length > 0)
            {
                var fileNames = string.Join(", ", addinFiles.Select(Path.GetFileName));
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Siemens",
                    Name = "Add-In Deployment",
                    Status = "OK",
                    Details = $"Deployed Add-In file(s): {fileNames}"
                });
            }
            else
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Siemens",
                    Name = "Add-In Deployment",
                    Status = "WARN",
                    Details = $"No .addin files found in '{userAddInsDir}'",
                    Recommendation = "Run 'tia-agent install' to deploy the TIA Portal Add-In."
                });
            }
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "Siemens",
                Name = "UserAddIns Directory",
                Status = "WARN",
                Details = $"UserAddIns directory does not exist at '{userAddInsDir}'",
                Recommendation = "Run 'tia-agent install' to create and deploy Add-In."
            });
        }
    }

    private static void CheckRuntimesAndMcp(TiaAgentLayout layout, DoctorReport report)
    {
        string defaultRuntime = "opencode";
        if (File.Exists(layout.ConfigPath))
        {
            try
            {
                var config = ManifestStore.Read<TiaAgentConfig>(layout.ConfigPath);
                defaultRuntime = config.DefaultRuntime ?? "opencode";
            }
            catch { }
        }

        var runtimesToCheck = new[] { "opencode", "mimo", "claude" };
        foreach (var runtimeId in runtimesToCheck)
        {
            var isDefault = string.Equals(runtimeId, defaultRuntime, StringComparison.OrdinalIgnoreCase);
            var onPath = IsExecutableOnPath(runtimeId);

            if (onPath)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Runtime",
                    Name = $"Agent Runtime ({runtimeId})",
                    Status = "OK",
                    Details = $"Executable '{runtimeId}' found on PATH." + (isDefault ? " (Default runtime)" : "")
                });
            }
            else if (isDefault)
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Runtime",
                    Name = $"Agent Runtime ({runtimeId})",
                    Status = "WARN",
                    Details = $"Default runtime '{runtimeId}' is not found on PATH.",
                    Recommendation = $"Install '{runtimeId}' CLI or update executable path in config.json."
                });
            }
            else
            {
                report.Checks.Add(new DoctorCheckResult
                {
                    Category = "Runtime",
                    Name = $"Agent Runtime ({runtimeId})",
                    Status = "OK",
                    Details = $"Optional runtime '{runtimeId}' not on PATH."
                });
            }
        }

        // Check MCP Server (tia-mcp or TiaMcpServer)
        var mcpOnPath = IsExecutableOnPath("tia-mcp") || IsExecutableOnPath("TiaMcpServer");
        if (mcpOnPath)
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "MCP",
                Name = "TIA MCP Server",
                Status = "OK",
                Details = "MCP server executable ('tia-mcp' / 'TiaMcpServer') found on PATH."
            });
        }
        else
        {
            report.Checks.Add(new DoctorCheckResult
            {
                Category = "MCP",
                Name = "TIA MCP Server",
                Status = "WARN",
                Details = "MCP server executable ('tia-mcp') not found on PATH.",
                Recommendation = "Install via: dotnet tool install -g TiaMcpServer"
            });
        }
    }

    public static bool IsExecutableOnPath(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return false;

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var extensions = isWindows ? new[] { "", ".exe", ".cmd", ".bat" } : new[] { "" };

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(dir, executableName + ext);
                if (File.Exists(fullPath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void PrintConsoleReport(DoctorReport report, TiaAgentLayout layout, TextWriter stdout, bool verbose)
    {
        stdout.WriteLine($"TIA Agent Doctor Diagnostics (v{report.ProductVersion})");
        stdout.WriteLine($"Root Path: {layout.RootPath}");
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
            if (verbose && !string.IsNullOrWhiteSpace(check.Recommendation))
            {
                stdout.WriteLine($"       Recommendation: {check.Recommendation}");
            }
        }

        stdout.WriteLine();
        stdout.WriteLine($"Diagnostic Summary: {report.Summary.Passed} passed, {report.Summary.Warnings} warnings, {report.Summary.Failed} failed.");
    }
}
