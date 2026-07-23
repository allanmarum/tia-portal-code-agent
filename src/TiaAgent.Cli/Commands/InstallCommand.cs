using System;
using System.Collections.Generic;
using System.IO;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Payload;

namespace TiaAgent.Cli.Commands;

public sealed class InstallOptions
{
    public string? Version { get; set; }
    public bool Force { get; set; }
    public string? PayloadDir { get; set; }
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
}

public static class InstallCommand
{
    public static int Execute(InstallOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var layout = new TiaAgentLayout(options.CustomRoot);
        layout.EnsureDirectoriesExist();

        var payloadDir = PayloadLocator.GetBundledPayloadDirectory(options.PayloadDir);
        var validation = PayloadValidator.ValidatePayload(payloadDir, options.Version);
        if (!validation.IsValid)
        {
            stderr.WriteLine("Payload validation failed:");
            foreach (var err in validation.Errors)
            {
                stderr.WriteLine($"  - {err}");
            }
            return 1;
        }

        var payloadManifest = validation.Manifest;
        if (payloadManifest is null)
        {
            stderr.WriteLine("Payload validation succeeded but manifest is missing.");
            return 1;
        }
        var targetVersion = !string.IsNullOrWhiteSpace(options.Version)
            ? options.Version
            : payloadManifest.ProductVersion;

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            targetVersion = Program.GetProductVersion();
        }

        var versionDir = layout.GetVersionPath(targetVersion);

        InstallationsManifest installations;
        try
        {
            installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        }
        catch
        {
            installations = new InstallationsManifest();
        }

        string? previousVersion = null;
        if (File.Exists(layout.CurrentManifestPath))
        {
            try
            {
                var existingCurrent = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
                if (!string.IsNullOrWhiteSpace(existingCurrent.ActiveVersion) &&
                    !string.Equals(existingCurrent.ActiveVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
                {
                    previousVersion = existingCurrent.ActiveVersion;
                }
                else
                {
                    previousVersion = existingCurrent.PreviousVersion;
                }
            }
            catch { }
        }

        if (installations.Versions.ContainsKey(targetVersion) && Directory.Exists(versionDir) && !options.Force)
        {
            var currentManifest = new CurrentManifest
            {
                SchemaVersion = 1,
                ActiveVersion = targetVersion,
                PreviousVersion = previousVersion,
                ActivatedAt = DateTimeOffset.UtcNow,
                ActivatedBy = "tia-agent install"
            };
            ManifestStore.WriteAtomic(layout.CurrentManifestPath, currentManifest);

            stdout.WriteLine($"TIA Agent version '{targetVersion}' is already installed at '{versionDir}'. Set as active version.");
            return 0;
        }

        if (options.Force && Directory.Exists(versionDir))
        {
            Directory.Delete(versionDir, recursive: true);
        }

        CopyDirectory(payloadDir, versionDir, overwrite: true);

        var componentsMeta = new Dictionary<string, ComponentFileMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var (compKey, compMeta) in payloadManifest.Components)
        {
            componentsMeta[compKey] = new ComponentFileMetadata
            {
                RelativePath = compMeta.RelativePath,
                Sha256Hash = compMeta.Sha256Hash,
                SizeBytes = compMeta.SizeBytes
            };
        }

        var versionMeta = new InstallationVersionMetadata
        {
            Version = targetVersion,
            InstalledAt = DateTimeOffset.UtcNow,
            CommitSha = payloadManifest.CommitSha,
            Components = componentsMeta
        };

        installations.Versions[targetVersion] = versionMeta;
        ManifestStore.WriteAtomic(layout.InstallationsManifestPath, installations);

        var activeCurrent = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = targetVersion,
            PreviousVersion = previousVersion,
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "tia-agent install"
        };
        ManifestStore.WriteAtomic(layout.CurrentManifestPath, activeCurrent);

        EnsureDefaultConfig(layout.ConfigPath, payloadDir);

        DeployAddInIfPresent(versionDir, payloadDir, options.UserAddInsDir, stdout);

        stdout.WriteLine($"Successfully installed TIA Agent v{targetVersion} to '{versionDir}'.");
        return 0;
    }

    private static void EnsureDefaultConfig(string configPath, string payloadDir)
    {
        if (File.Exists(configPath))
        {
            return;
        }

        var payloadConfigDir = Path.Combine(payloadDir, "config");
        if (Directory.Exists(payloadConfigDir))
        {
            var settingsExample = Path.Combine(payloadConfigDir, "settings.example.json");
            if (File.Exists(settingsExample))
            {
                File.Copy(settingsExample, configPath, overwrite: true);
                return;
            }
        }

        var defaultConfigJson = """
        {
          "defaultRuntime": "opencode"
        }
        """;
        File.WriteAllText(configPath, defaultConfigJson);
    }

    private static void DeployAddInIfPresent(string versionDir, string payloadDir, string? customUserAddInsDir, TextWriter stdout)
    {
        var userAddInsDir = customUserAddInsDir;
        if (string.IsNullOrWhiteSpace(userAddInsDir))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            userAddInsDir = Path.Combine(appData, "Siemens", "Automation", "Portal V21", "UserAddIns");
        }

        var searchDirs = new[]
        {
            Path.Combine(versionDir, "AddIn"),
            Path.Combine(payloadDir, "AddIn")
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var addinFiles = Directory.GetFiles(dir, "*.addin");
            if (addinFiles.Length > 0)
            {
                Directory.CreateDirectory(userAddInsDir);
                foreach (var addinFile in addinFiles)
                {
                    var destFile = Path.Combine(userAddInsDir, Path.GetFileName(addinFile));
                    File.Copy(addinFile, destFile, overwrite: true);
                    stdout.WriteLine($"Deployed Add-In artifact '{Path.GetFileName(addinFile)}' to '{userAddInsDir}'.");
                }
                break;
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir, overwrite);
        }
    }
}
