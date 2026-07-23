using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaAgent.Cli.Layout;

namespace TiaAgent.Cli.Commands;

public sealed class UninstallOptions
{
    public string? Version { get; set; }
    public bool All { get; set; }
    public bool Force { get; set; }
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
}

public static class UninstallCommand
{
    public static int Execute(UninstallOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

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

        var targetVersions = new List<string>();

        if (options.All)
        {
            targetVersions.AddRange(installations.Versions.Keys);
        }
        else if (!string.IsNullOrWhiteSpace(options.Version))
        {
            if (!installations.Versions.ContainsKey(options.Version) && !options.Force)
            {
                stderr.WriteLine($"Version '{options.Version}' is not installed.");
                return 1;
            }
            targetVersions.Add(options.Version);
        }
        else if (!string.IsNullOrWhiteSpace(current.ActiveVersion))
        {
            targetVersions.Add(current.ActiveVersion);
        }
        else if (installations.Versions.Count > 0)
        {
            stderr.WriteLine("No active version found and no version specified. Specify --version <version> or --all.");
            return 1;
        }
        else
        {
            stdout.WriteLine("No installed TIA Agent versions found.");
            return 0;
        }

        var userAddInsDir = options.UserAddInsDir;
        if (string.IsNullOrWhiteSpace(userAddInsDir))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            userAddInsDir = Path.Combine(appData, "Siemens", "Automation", "Portal V21", "UserAddIns");
        }

        var uninstalledVersions = new List<string>();

        foreach (var ver in targetVersions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var versionDir = layout.GetVersionPath(ver);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, recursive: true);
                }

                RemoveAddInFilesForVersion(ver, userAddInsDir, stdout);

                installations.Versions.Remove(ver);
                uninstalledVersions.Add(ver);
            }
            catch (Exception ex)
            {
                if (options.Force)
                {
                    stderr.WriteLine($"Warning: Failed to cleanly remove version '{ver}': {ex.Message}");
                    installations.Versions.Remove(ver);
                    uninstalledVersions.Add(ver);
                }
                else
                {
                    stderr.WriteLine($"Error removing version '{ver}': {ex.Message}");
                    return 1;
                }
            }
        }

        ManifestStore.WriteAtomic(layout.InstallationsManifestPath, installations);

        if (uninstalledVersions.Contains(current.ActiveVersion, StringComparer.OrdinalIgnoreCase))
        {
            if (installations.Versions.Count > 0)
            {
                var nextActive = installations.Versions.Keys.First();
                var newCurrent = new CurrentManifest
                {
                    SchemaVersion = 1,
                    ActiveVersion = nextActive,
                    ActivatedAt = DateTimeOffset.UtcNow,
                    ActivatedBy = "tia-agent uninstall"
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

        stdout.WriteLine($"Successfully uninstalled TIA Agent version(s): {string.Join(", ", uninstalledVersions)}.");
        return 0;
    }

    private static void RemoveAddInFilesForVersion(string version, string userAddInsDir, TextWriter stdout)
    {
        if (!Directory.Exists(userAddInsDir))
        {
            return;
        }

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
                catch { }
            }
        }
    }
}
