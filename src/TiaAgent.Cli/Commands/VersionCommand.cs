using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using TiaAgent.Cli.Layout;

namespace TiaAgent.Cli.Commands;

public sealed class VersionOptions
{
    public bool Verbose { get; set; }
    public bool Json { get; set; }
    public string? CustomRoot { get; set; }
}

public sealed class VersionReport
{
    public string ProductVersion { get; set; } = string.Empty;
    public string? ActiveVersion { get; set; }
    public List<VersionDetail> InstalledVersions { get; set; } = new();
    public string ConfigPath { get; set; } = string.Empty;
    public string OsEnvironment { get; set; } = string.Empty;
    public string DotnetFramework { get; set; } = string.Empty;
}

public sealed class VersionDetail
{
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; }
    public string? CommitSha { get; set; }
}

public static class VersionCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(VersionOptions options, TextWriter? stdout = null)
    {
        stdout ??= Console.Out;

        var layout = new TiaAgentLayout(options.CustomRoot);
        var productVersion = Program.GetProductVersion();

        string? activeVersion = null;
        if (File.Exists(layout.CurrentManifestPath))
        {
            try
            {
                var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
                activeVersion = current.ActiveVersion;
            }
            catch { }
        }

        var installedVersions = new List<VersionDetail>();
        if (File.Exists(layout.InstallationsManifestPath))
        {
            try
            {
                var installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
                foreach (var (ver, meta) in installations.Versions)
                {
                    installedVersions.Add(new VersionDetail
                    {
                        Version = ver,
                        InstalledAt = meta.InstalledAt,
                        CommitSha = meta.CommitSha
                    });
                }
            }
            catch { }
        }

        var report = new VersionReport
        {
            ProductVersion = productVersion,
            ActiveVersion = activeVersion,
            InstalledVersions = installedVersions,
            ConfigPath = layout.ConfigPath,
            OsEnvironment = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            DotnetFramework = RuntimeInformation.FrameworkDescription
        };

        if (options.Json)
        {
            var json = JsonSerializer.Serialize(report, s_jsonOptions);
            stdout.WriteLine(json);
            return 0;
        }

        if (options.Verbose)
        {
            stdout.WriteLine($"TIA Agent CLI (tia-agent)");
            stdout.WriteLine($"Product Version:     {report.ProductVersion}");
            stdout.WriteLine($"Active Version:      {report.ActiveVersion ?? "(none)"}");
            stdout.WriteLine($"Configuration Path:  {report.ConfigPath}");
            stdout.WriteLine($"OS Environment:      {report.OsEnvironment}");
            stdout.WriteLine($".NET Framework:      {report.DotnetFramework}");
            stdout.WriteLine();

            if (report.InstalledVersions.Count == 0)
            {
                stdout.WriteLine("Installed Versions:  (none)");
            }
            else
            {
                stdout.WriteLine("Installed Versions:");
                foreach (var ver in report.InstalledVersions)
                {
                    var isCurrent = string.Equals(ver.Version, report.ActiveVersion, StringComparison.OrdinalIgnoreCase);
                    var activeTag = isCurrent ? " [active]" : "";
                    var commit = !string.IsNullOrWhiteSpace(ver.CommitSha) ? $" (Commit: {ver.CommitSha})" : "";
                    stdout.WriteLine($"  - {ver.Version}{activeTag} (Installed: {ver.InstalledAt:yyyy-MM-dd HH:mm:ss}){commit}");
                }
            }
        }
        else
        {
            stdout.WriteLine($"tia-agent version {productVersion}");
        }

        return 0;
    }
}
