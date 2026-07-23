using System;
using System.IO;
using System.Text.Json;
using TiaAgent.Cli.Release;

namespace TiaAgent.Cli.Commands;

public sealed class GenerateReleaseMetadataOptions
{
    public string Dir { get; set; } = "artifacts";
    public string? Version { get; set; }
    public string? CommitSha { get; set; }
    public string? RepoRoot { get; set; }
    public bool Json { get; set; }
}

public static class GenerateReleaseMetadataCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(GenerateReleaseMetadataOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var releaseDir = options.Dir;
        if (!Path.IsPathRooted(releaseDir))
        {
            releaseDir = Path.Combine(Directory.GetCurrentDirectory(), releaseDir);
        }

        var version = !string.IsNullOrWhiteSpace(options.Version)
            ? options.Version
            : Program.GetProductVersion();

        var commitSha = !string.IsNullOrWhiteSpace(options.CommitSha)
            ? options.CommitSha
            : "unknown";

        try
        {
            var manifest = ReleaseGenerator.GenerateReleaseMetadata(
                artifactsDirectory: releaseDir,
                productVersion: version,
                commitSha: commitSha,
                repoRoot: options.RepoRoot ?? Directory.GetCurrentDirectory(),
                timestamp: DateTimeOffset.UtcNow);

            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(manifest, s_jsonOptions));
            }
            else
            {
                stdout.WriteLine($"Successfully generated release metadata in '{releaseDir}':");
                stdout.WriteLine($"  Product Version: {manifest.ProductVersion}");
                stdout.WriteLine($"  Channel:         {manifest.Channel}");
                stdout.WriteLine($"  Commit SHA:      {manifest.CommitSha}");
                stdout.WriteLine($"  Artifacts Count: {manifest.Artifacts.Count}");
                stdout.WriteLine($"  Manifest File:   {ReleaseStore.ManifestFileName}");
                stdout.WriteLine($"  Checksums File:  {ReleaseStore.ChecksumsFileName}");
                stdout.WriteLine($"  SBOM File:       {ReleaseStore.SbomFileName}");
                stdout.WriteLine($"  Notices File:    {ReleaseStore.NoticesFileName}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Failed to generate release metadata: {ex.Message}");
            return 1;
        }
    }
}
