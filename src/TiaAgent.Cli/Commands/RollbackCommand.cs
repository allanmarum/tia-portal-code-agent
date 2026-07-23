using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaAgent.Cli.Layout;

namespace TiaAgent.Cli.Commands;

public sealed class RollbackOptions
{
    public string? Version { get; set; }
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
    public bool Force { get; set; }
    public bool Json { get; set; }
}

public sealed class RollbackReport
{
    public bool Success { get; set; }
    public string ActiveVersion { get; set; } = string.Empty;
    public string? PreviousVersion { get; set; }
    public DateTimeOffset RolledBackAt { get; set; }
    public string VersionPath { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public static class RollbackCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(RollbackOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var layout = new TiaAgentLayout(options.CustomRoot);
        layout.EnsureDirectoriesExist();

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

        string? targetVersion = options.Version;

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            if (!string.IsNullOrWhiteSpace(current.PreviousVersion) &&
                installations.Versions.ContainsKey(current.PreviousVersion))
            {
                targetVersion = current.PreviousVersion;
            }
            else
            {
                var candidates = installations.Versions
                    .Where(kv => !string.Equals(kv.Key, current.ActiveVersion, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kv => kv.Value.InstalledAt)
                    .Select(kv => kv.Key)
                    .ToList();

                if (candidates.Count > 0)
                {
                    targetVersion = candidates[0];
                }
            }
        }

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            var err = "No previous version available for rollback.";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new RollbackReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
            }
            return 1;
        }

        var versionDir = layout.GetVersionPath(targetVersion);
        bool isInstalled = installations.Versions.ContainsKey(targetVersion) && Directory.Exists(versionDir);

        if (!isInstalled && !options.Force)
        {
            var err = $"Rollback target version '{targetVersion}' is not installed.";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new RollbackReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
            }
            return 1;
        }

        var previousActive = current.ActiveVersion;

        var updatedCurrent = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = targetVersion,
            PreviousVersion = previousActive,
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "tia-agent rollback"
        };

        ManifestStore.WriteAtomic(layout.CurrentManifestPath, updatedCurrent);
        DeployAddInIfPresent(versionDir, options.UserAddInsDir, options.Json ? TextWriter.Null : stdout);

        var report = new RollbackReport
        {
            Success = true,
            ActiveVersion = targetVersion,
            PreviousVersion = previousActive,
            RolledBackAt = updatedCurrent.ActivatedAt,
            VersionPath = versionDir
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
        }
        else
        {
            stdout.WriteLine($"Successfully rolled back TIA Agent to version '{targetVersion}'.");
        }

        return 0;
    }

    private static void DeployAddInIfPresent(string versionDir, string? customUserAddInsDir, TextWriter stdout)
    {
        if (!Directory.Exists(versionDir))
        {
            return;
        }

        var userAddInsDir = customUserAddInsDir;
        if (string.IsNullOrWhiteSpace(userAddInsDir))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            userAddInsDir = Path.Combine(appData, "Siemens", "Automation", "Portal V21", "UserAddIns");
        }

        var addinSubDir = Path.Combine(versionDir, "AddIn");
        if (Directory.Exists(addinSubDir))
        {
            var addinFiles = Directory.GetFiles(addinSubDir, "*.addin");
            if (addinFiles.Length > 0)
            {
                Directory.CreateDirectory(userAddInsDir);
                foreach (var addinFile in addinFiles)
                {
                    var destFile = Path.Combine(userAddInsDir, Path.GetFileName(addinFile));
                    File.Copy(addinFile, destFile, overwrite: true);
                    stdout.WriteLine($"Deployed Add-In artifact '{Path.GetFileName(addinFile)}' to '{userAddInsDir}'.");
                }
            }
        }
    }
}
