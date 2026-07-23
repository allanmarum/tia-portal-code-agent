using System;
using System.IO;
using System.Text.Json;
using TiaAgent.Cli.Release;

namespace TiaAgent.Cli.Commands;

public sealed class VerifyReleaseOptions
{
    public string Dir { get; set; } = "artifacts";
    public string? Version { get; set; }
    public bool Json { get; set; }
    public bool Verbose { get; set; }
}

public sealed class VerifyReleaseReport
{
    public bool Success { get; set; }
    public string ProductVersion { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public int ArtifactCount { get; set; }
    public string Directory { get; set; } = string.Empty;
    public string[] Errors { get; set; } = Array.Empty<string>();
}

public static class VerifyReleaseCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(VerifyReleaseOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var releaseDir = options.Dir;
        if (!Path.IsPathRooted(releaseDir))
        {
            releaseDir = Path.Combine(Directory.GetCurrentDirectory(), releaseDir);
        }

        var validation = ReleaseValidator.ValidateRelease(releaseDir, options.Version);
        var report = new VerifyReleaseReport
        {
            Success = validation.IsValid,
            Directory = releaseDir,
            Errors = validation.Errors.ToArray()
        };

        if (validation.IsValid && validation.Manifest != null)
        {
            report.ProductVersion = validation.Manifest.ProductVersion;
            report.Channel = validation.Manifest.Channel;
            report.CommitSha = validation.Manifest.CommitSha;
            report.ArtifactCount = validation.Manifest.Artifacts.Count;
        }

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
        }
        else
        {
            if (validation.IsValid && validation.Manifest != null)
            {
                stdout.WriteLine($"Release validation PASSED for directory '{releaseDir}'.");
                stdout.WriteLine($"  Product Version: {validation.Manifest.ProductVersion}");
                stdout.WriteLine($"  Channel:         {validation.Manifest.Channel}");
                stdout.WriteLine($"  Commit SHA:      {validation.Manifest.CommitSha}");
                stdout.WriteLine($"  Artifact Count:  {validation.Manifest.Artifacts.Count}");
                if (options.Verbose)
                {
                    stdout.WriteLine("  Artifacts:");
                    foreach (var art in validation.Manifest.Artifacts)
                    {
                        stdout.WriteLine($"    - {art.Name} ({art.SizeBytes} bytes, SHA256: {art.Sha256Hash})");
                    }
                }
            }
            else
            {
                stderr.WriteLine($"Release validation FAILED for directory '{releaseDir}':");
                foreach (var err in validation.Errors)
                {
                    stderr.WriteLine($"  - {err}");
                }
            }
        }

        return validation.IsValid ? 0 : 1;
    }
}
