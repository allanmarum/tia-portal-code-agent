using System;
using System.IO;
using System.Text.Json;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Payload;
using TiaAgent.Cli.Release;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Commands;

public sealed class UpdateOptions
{
    public string? Version { get; set; }
    public string? PayloadDir { get; set; }
    public string? CustomRoot { get; set; }
    public string? UserAddInsDir { get; set; }
    public bool Force { get; set; }
    public bool Json { get; set; }
}

public sealed class UpdateReport
{
    public bool Success { get; set; }
    public string ActiveVersion { get; set; } = string.Empty;
    public string? PreviousVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string VersionPath { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public static class UpdateCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(UpdateOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
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

        string? payloadDir = null;
        PayloadManifest? payloadManifest = null;
        try
        {
            payloadDir = PayloadLocator.GetBundledPayloadDirectory(options.PayloadDir);
            var validation = PayloadValidator.ValidatePayload(payloadDir, options.Version);
            if (validation.IsValid)
            {
                payloadManifest = validation.Manifest;
            }
        }
        catch (IOException)
        {
            // Optional payload discovery
        }
        catch (UnauthorizedAccessException)
        {
            // Optional payload discovery
        }

        string? targetVersion = options.Version;
        if (string.IsNullOrWhiteSpace(targetVersion) && payloadManifest != null)
        {
            targetVersion = payloadManifest.ProductVersion;
        }

        // Channel validation: warn if the target version is incompatible with the configured channel
        if (!string.IsNullOrWhiteSpace(targetVersion))
        {
            TiaAgentConfig config;
            try
            {
                config = ManifestStore.Read<TiaAgentConfig>(layout.ConfigPath);
            }
            catch
            {
                config = new TiaAgentConfig();
            }

            var channel = ChannelUtils.NormalizeChannel(config.UpdateChannel) ?? "stable";
            if (!ChannelUtils.IsVersionCompatibleWithChannel(targetVersion, channel))
            {
                var versionChannel = ReleaseStore.ResolveChannel(targetVersion);
                var err = $"Version '{targetVersion}' (channel: {versionChannel}) is not compatible with the configured update channel '{channel}'. Use --force to override.";
                if (options.Force)
                {
                    if (!options.Json)
                    {
                        stderr.WriteLine($"WARNING: {err}");
                    }
                }
                else
                {
                    if (options.Json)
                    {
                        stdout.WriteLine(JsonSerializer.Serialize(new UpdateReport { Success = false, Error = err }, s_jsonOptions));
                    }
                    else
                    {
                        stderr.WriteLine(err);
                    }
                    return 1;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            var err = "Version to update must be specified or a valid payload must be available.";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new UpdateReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
            }
            return 1;
        }

        var versionDir = layout.GetVersionPath(targetVersion);
        bool isInstalled = installations.Versions.ContainsKey(targetVersion) && Directory.Exists(versionDir);

        if (!isInstalled)
        {
            if (payloadManifest != null && payloadDir != null)
            {
                var installOptions = new InstallOptions
                {
                    Version = targetVersion,
                    PayloadDir = payloadDir,
                    CustomRoot = options.CustomRoot,
                    UserAddInsDir = options.UserAddInsDir,
                    Force = options.Force
                };

                using var subStdout = new StringWriter();
                using var subStderr = new StringWriter();
                var targetStdout = options.Json ? subStdout : stdout;
                var targetStderr = options.Json ? subStderr : stderr;
                var installResult = InstallCommand.Execute(installOptions, targetStdout, targetStderr);

                if (installResult != 0)
                {
                    var err = options.Json ? subStderr.ToString().Trim() : null;
                    if (options.Json)
                    {
                        stdout.WriteLine(JsonSerializer.Serialize(new UpdateReport { Success = false, Error = string.IsNullOrWhiteSpace(err) ? "Installation failed during update." : err }, s_jsonOptions));
                    }
                    return installResult;
                }

                try
                {
                    current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
                }
                catch { }
            }
            else if (!options.Force)
            {
                var err = $"Version '{targetVersion}' is not installed and no valid payload was found to install it.";
                if (options.Json)
                {
                    stdout.WriteLine(JsonSerializer.Serialize(new UpdateReport { Success = false, Error = err }, s_jsonOptions));
                }
                else
                {
                    stderr.WriteLine(err);
                }
                return 1;
            }
        }

        bool isAlreadyActive = string.Equals(current.ActiveVersion, targetVersion, StringComparison.OrdinalIgnoreCase);
        if (isAlreadyActive && !options.Force)
        {
            var report = new UpdateReport
            {
                Success = true,
                ActiveVersion = targetVersion,
                PreviousVersion = current.PreviousVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                VersionPath = versionDir
            };

            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
            }
            else
            {
                stdout.WriteLine($"TIA Agent version '{targetVersion}' is already active.");
            }
            return 0;
        }

        string? previousVersion = (!string.IsNullOrWhiteSpace(current.ActiveVersion) && !isAlreadyActive)
            ? current.ActiveVersion
            : current.PreviousVersion;

        var updatedCurrent = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = targetVersion,
            PreviousVersion = previousVersion,
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "tia-agent update"
        };

        ManifestStore.WriteAtomic(layout.CurrentManifestPath, updatedCurrent);
        CommandHelpers.DeployAddInIfPresent(versionDir, options.UserAddInsDir, options.Json ? TextWriter.Null : stdout);

        var finalReport = new UpdateReport
        {
            Success = true,
            ActiveVersion = targetVersion,
            PreviousVersion = previousVersion,
            UpdatedAt = updatedCurrent.ActivatedAt,
            VersionPath = versionDir
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(finalReport, s_jsonOptions));
        }
        else
        {
            stdout.WriteLine($"Successfully updated TIA Agent to version '{targetVersion}'.");
        }

        return 0;
    }
}
