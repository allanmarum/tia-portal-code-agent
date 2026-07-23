using System;
using System.Collections.Generic;

namespace TiaAgent.Cli.Release;

/// <summary>
/// Root schema for release-manifest.json included with distributable release artifacts.
/// </summary>
public sealed class ReleaseManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string ProductVersion { get; init; } = string.Empty;
    public string Channel { get; init; } = "stable";
    public string CommitSha { get; init; } = "unknown";
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public ReleaseCompatibilityMetadata Compatibility { get; init; } = new();
    public Dictionary<string, ReleaseComponentMetadata> Components { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ReleaseArtifactEntry> Artifacts { get; init; } = new();
}

/// <summary>
/// Target Siemens TIA Portal and runtime compatibility declarations.
/// </summary>
public sealed class ReleaseCompatibilityMetadata
{
    public string TiaPortalVersion { get; init; } = "V21";
    public string OpennessVersion { get; init; } = "V21";
    public string TargetFramework { get; init; } = "net8.0";
}

/// <summary>
/// Metadata for key components in the release (Bridge, AddIn, CLI).
/// </summary>
public sealed class ReleaseComponentMetadata
{
    public string RelativePath { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

/// <summary>
/// Record of a published artifact in the distribution set.
/// </summary>
public sealed class ReleaseArtifactEntry
{
    public string Name { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Component { get; init; } = string.Empty;
}

/// <summary>
/// Result of validating a release distribution.
/// </summary>
public sealed class ReleaseValidationResult
{
    public bool IsValid { get; init; }
    public ReleaseManifest? Manifest { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ReleaseValidationResult Success(ReleaseManifest manifest) =>
        new() { IsValid = true, Manifest = manifest };

    public static ReleaseValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors };

    public static ReleaseValidationResult Failure(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = new List<string>(errors) };
}
