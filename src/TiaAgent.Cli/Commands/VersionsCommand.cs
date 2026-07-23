using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Release;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Commands;

public sealed class VersionsOptions
{
    public string? Subcommand { get; set; }
    public string? Version { get; set; }
    public bool Force { get; set; }
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
    public bool Json { get; set; }
    public bool Verbose { get; set; }
}

public sealed class VersionsReport
{
    public string ProductVersion { get; set; } = string.Empty;
    public string? ActiveVersion { get; set; }
    public string? RollbackVersion { get; set; }
    public string UpdateChannel { get; set; } = "stable";
    public List<VersionsItemDetail> InstalledVersions { get; set; } = new();
}

public sealed class VersionsItemDetail
{
    public string Version { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsRollbackCandidate { get; set; }
    public DateTimeOffset InstalledAt { get; set; }
    public string CommitSha { get; set; } = "unknown";
    public string Channel { get; set; } = "stable";
}

public static class VersionsCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(VersionsOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var sub = (options.Subcommand ?? "list").ToLowerInvariant();
        return sub switch
        {
            "list" or "ls" => ExecuteList(options, stdout),
            "remove" or "rm" or "delete" => ExecuteRemove(options, stdout, stderr),
            _ => ExecuteList(options, stdout)
        };
    }

    private static int ExecuteList(VersionsOptions options, TextWriter stdout)
    {
        var layout = new TiaAgentLayout(options.CustomRoot);
        var productVersion = Program.GetProductVersion();

        CurrentManifest current;
        try
        {
            current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        }
        catch
        {
            current = new CurrentManifest();
        }

        InstallationsManifest installations;
        try
        {
            installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        }
        catch
        {
            installations = new InstallationsManifest();
        }

        string? rollbackVersion = current.PreviousVersion;
        if (string.IsNullOrWhiteSpace(rollbackVersion))
        {
            rollbackVersion = installations.Versions.Keys
                .Where(v => !string.Equals(v, current.ActiveVersion, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => installations.Versions[v].InstalledAt)
                .FirstOrDefault();
        }

        var items = new List<VersionsItemDetail>();
        foreach (var (ver, meta) in installations.Versions)
        {
            bool isActive = string.Equals(ver, current.ActiveVersion, StringComparison.OrdinalIgnoreCase);
            bool isRollback = string.Equals(ver, rollbackVersion, StringComparison.OrdinalIgnoreCase) && !isActive;

            items.Add(new VersionsItemDetail
            {
                Version = ver,
                IsActive = isActive,
                IsRollbackCandidate = isRollback,
                InstalledAt = meta.InstalledAt,
                CommitSha = meta.CommitSha,
                Channel = ReleaseStore.ResolveChannel(ver)
            });
        }

        TiaAgentConfig config;
        try
        {
            config = ManifestStore.Read<TiaAgentConfig>(layout.ConfigPath);
        }
        catch
        {
            config = new TiaAgentConfig();
        }

        var updateChannel = ChannelUtils.NormalizeChannel(config.UpdateChannel) ?? "stable";

        var report = new VersionsReport
        {
            ProductVersion = productVersion,
            ActiveVersion = current.ActiveVersion,
            RollbackVersion = rollbackVersion,
            UpdateChannel = updateChannel,
            InstalledVersions = items
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
            return 0;
        }

        stdout.WriteLine($"TIA Agent Versions (CLI Product v{productVersion})");
        stdout.WriteLine($"Active Version:    {report.ActiveVersion ?? "(none)"}");
        stdout.WriteLine($"Rollback Version:  {report.RollbackVersion ?? "(none)"}");
        stdout.WriteLine($"Update Channel:    {report.UpdateChannel}");
        stdout.WriteLine();

        if (report.InstalledVersions.Count == 0)
        {
            stdout.WriteLine("No TIA Agent versions are currently installed.");
        }
        else
        {
            stdout.WriteLine("Installed Versions:");
            foreach (var item in report.InstalledVersions)
            {
                var tags = new List<string>();
                if (item.IsActive) tags.Add("active");
                if (item.IsRollbackCandidate) tags.Add("rollback candidate");
                var tagStr = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
                var commitStr = !string.IsNullOrWhiteSpace(item.CommitSha) && item.CommitSha != "unknown" ? $" (Commit: {item.CommitSha})" : "";
                var channelStr = item.Channel != "stable" ? $" [{item.Channel}]" : "";

                stdout.WriteLine($"  - {item.Version}{channelStr}{tagStr} (Installed: {item.InstalledAt:yyyy-MM-dd HH:mm:ss}){commitStr}");
            }
        }

        return 0;
    }

    private static int ExecuteRemove(VersionsOptions options, TextWriter stdout, TextWriter stderr)
    {
        var layout = new TiaAgentLayout(options.CustomRoot);

        InstallationsManifest installations;
        try
        {
            installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        }
        catch
        {
            installations = new InstallationsManifest();
        }

        CurrentManifest current;
        try
        {
            current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        }
        catch
        {
            current = new CurrentManifest();
        }

        var targetVersion = options.Version;
        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            stderr.WriteLine("Version to remove must be specified. Usage: tia-agent versions remove <version>");
            return 1;
        }

        if (!installations.Versions.ContainsKey(targetVersion) && !options.Force)
        {
            stderr.WriteLine($"Version '{targetVersion}' is not installed.");
            return 1;
        }

        // Controlled version removal check 1: Active version
        bool isActive = string.Equals(targetVersion, current.ActiveVersion, StringComparison.OrdinalIgnoreCase);
        if (isActive && !options.Force)
        {
            stderr.WriteLine($"Cannot remove version '{targetVersion}' because it is currently active. Switch active version first or use --force.");
            return 1;
        }

        // Controlled version removal check 2: Preservation of at least one known-good rollback version
        var otherInstalled = installations.Versions.Keys
            .Where(v => !string.Equals(v, targetVersion, StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool isActiveSet = !string.IsNullOrWhiteSpace(current.ActiveVersion) && !isActive;
        bool leavesNoRollback = isActiveSet && otherInstalled.All(v => string.Equals(v, current.ActiveVersion, StringComparison.OrdinalIgnoreCase));

        if (leavesNoRollback && !options.Force)
        {
            stderr.WriteLine($"Cannot remove version '{targetVersion}' because it is the only known-good rollback version. Use --force to override preservation rule.");
            return 1;
        }

        var userAddInsDir = options.UserAddInsDir;
        if (string.IsNullOrWhiteSpace(userAddInsDir))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            userAddInsDir = Path.Combine(appData, "Siemens", "Automation", "Portal V21", "UserAddIns");
        }

        try
        {
            var versionDir = layout.GetVersionPath(targetVersion);
            if (Directory.Exists(versionDir))
            {
                Directory.Delete(versionDir, recursive: true);
            }

            RemoveAddInFilesForVersion(targetVersion, userAddInsDir, stdout, stderr);
            installations.Versions.Remove(targetVersion);
            ManifestStore.WriteAtomic(layout.InstallationsManifestPath, installations);
        }
        catch (Exception ex)
        {
            if (options.Force)
            {
                stderr.WriteLine($"Warning: Failed to cleanly remove version '{targetVersion}': {ex.Message}");
                installations.Versions.Remove(targetVersion);
                ManifestStore.WriteAtomic(layout.InstallationsManifestPath, installations);
            }
            else
            {
                stderr.WriteLine($"Error removing version '{targetVersion}': {ex.Message}");
                return 1;
            }
        }

        // Handle active version update if active version was removed with --force
        if (isActive)
        {
            var remainingVersions = installations.Versions.Keys.ToList();
            if (remainingVersions.Count > 0)
            {
                var nextActive = remainingVersions.First();
                var newCurrent = new CurrentManifest
                {
                    SchemaVersion = 1,
                    ActiveVersion = nextActive,
                    PreviousVersion = null,
                    ActivatedAt = DateTimeOffset.UtcNow,
                    ActivatedBy = "tia-agent versions remove"
                };
                ManifestStore.WriteAtomic(layout.CurrentManifestPath, newCurrent);
                stdout.WriteLine($"Switched active version to '{nextActive}'.");
            }
            else
            {
                if (File.Exists(layout.CurrentManifestPath))
                {
                    try { File.Delete(layout.CurrentManifestPath); } catch { }
                }
            }
        }
        else if (string.Equals(targetVersion, current.PreviousVersion, StringComparison.OrdinalIgnoreCase))
        {
            // Update PreviousVersion if targetVersion was previous
            var newPrevious = installations.Versions.Keys
                .Where(v => !string.Equals(v, current.ActiveVersion, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => installations.Versions[v].InstalledAt)
                .FirstOrDefault();

            var newCurrent = new CurrentManifest
            {
                SchemaVersion = 1,
                ActiveVersion = current.ActiveVersion,
                PreviousVersion = newPrevious,
                ActivatedAt = current.ActivatedAt,
                ActivatedBy = current.ActivatedBy
            };
            ManifestStore.WriteAtomic(layout.CurrentManifestPath, newCurrent);
        }

        stdout.WriteLine($"Successfully removed TIA Agent version '{targetVersion}'.");
        return 0;
    }

    private static void RemoveAddInFilesForVersion(string version, string userAddInsDir, TextWriter stdout, TextWriter stderr)
    {
        if (!Directory.Exists(userAddInsDir)) return;

        var pubVersion = version.Split('-')[0];
        var candidates = Directory.GetFiles(userAddInsDir, "*.addin");
        foreach (var file in candidates)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Contains(version, StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains(pubVersion, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(file);
                    stdout.WriteLine($"Removed Add-In artifact '{fileName}' from '{userAddInsDir}'.");
                }
                catch (Exception ex)
                {
                    stderr.WriteLine($"Warning: Failed to remove Add-In artifact '{fileName}': {ex.Message}");
                }
            }
        }
    }
}
