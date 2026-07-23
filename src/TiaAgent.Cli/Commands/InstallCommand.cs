using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiaAgent.Cli.Installation;
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
        catch (FileNotFoundException)
        {
            installations = new InstallationsManifest();
        }
        catch (DirectoryNotFoundException)
        {
            installations = new InstallationsManifest();
        }
        catch (JsonException)
        {
            installations = new InstallationsManifest();
        }
        catch (IOException)
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
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (JsonException) { }
            catch (IOException) { }
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
            TryDeleteDirectory(versionDir, stdout, stderr);
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

        var deploymentResult = AddInDeployer.Deploy(versionDir, options.UserAddInsDir, layout.RootPath, stdout);

        stdout.WriteLine();
        stdout.WriteLine($"Successfully installed TIA Agent v{targetVersion} to '{versionDir}'.");

        if (!deploymentResult.IsFullyDeployed)
        {
            stdout.WriteLine();
            stdout.WriteLine("NOTE: The TIA Portal Add-In was not deployed to the UserAddIns directory.");
            if (deploymentResult.FallbackAddInPath != null)
            {
                stdout.WriteLine($"The Add-In is available at: {deploymentResult.FallbackAddInPath}");
            }
        }

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

    private static void TryDeleteDirectory(string dir, TextWriter stdout, TextWriter stderr)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                stdout.WriteLine($"Directory locked, retrying deletion ({attempt}/{maxRetries})...");
                System.Threading.Thread.Sleep(1000);
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries)
            {
                stdout.WriteLine($"Directory locked, retrying deletion ({attempt}/{maxRetries})...");
                System.Threading.Thread.Sleep(1000);
            }
        }

        // Final attempt — let it throw if it fails
        Directory.Delete(dir, recursive: true);
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
