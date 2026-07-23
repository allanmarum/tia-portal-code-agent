using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaAgent.Cli.Layout;

namespace TiaAgent.Cli.Commands;

public sealed class ActivateOptions
{
    public string? Version { get; set; }
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
    public bool Json { get; set; }
    public bool Force { get; set; }
}

public sealed class ActivateReport
{
    public bool Success { get; set; }
    public string ActiveVersion { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; set; }
    public string ActivatedBy { get; set; } = string.Empty;
    public string VersionPath { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public static class ActivateCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(ActivateOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var layout = new TiaAgentLayout(options.CustomRoot);
        layout.EnsureDirectoriesExist();

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            var err = "Version to activate must be specified. Usage: tia-agent activate <version>";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new ActivateReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
            }
            return 1;
        }

        var targetVersion = options.Version;
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

        bool isRegistered = installations.Versions.ContainsKey(targetVersion);
        bool directoryExists = Directory.Exists(versionDir);

        if ((!isRegistered || !directoryExists) && !options.Force)
        {
            var err = $"Version '{targetVersion}' is not installed.";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new ActivateReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
                if (installations.Versions.Count > 0)
                {
                    stderr.WriteLine($"Installed versions: {string.Join(", ", installations.Versions.Keys)}");
                }
                else
                {
                    stderr.WriteLine("No TIA Agent versions are currently installed.");
                }
            }
            return 1;
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

        var currentManifest = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = targetVersion,
            PreviousVersion = previousVersion,
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "tia-agent activate"
        };

        ManifestStore.WriteAtomic(layout.CurrentManifestPath, currentManifest);

        CommandHelpers.DeployAddInIfPresent(versionDir, options.UserAddInsDir, options.Json ? TextWriter.Null : stdout);

        var report = new ActivateReport
        {
            Success = true,
            ActiveVersion = targetVersion,
            ActivatedAt = currentManifest.ActivatedAt,
            ActivatedBy = currentManifest.ActivatedBy,
            VersionPath = versionDir
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
        }
        else
        {
            stdout.WriteLine($"Successfully activated TIA Agent version '{targetVersion}'.");
        }

        return 0;
    }
}
