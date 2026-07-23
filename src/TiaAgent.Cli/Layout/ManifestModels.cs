using System;
using System.Collections.Generic;

namespace TiaAgent.Cli.Layout;

/// <summary>
/// Schema for current.json - tracks active installation version.
/// </summary>
public sealed class CurrentManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string ActiveVersion { get; init; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ActivatedBy { get; init; }
}

/// <summary>
/// Schema for installations.json - tracks all installed versions.
/// </summary>
public sealed class InstallationsManifest
{
    public int SchemaVersion { get; init; } = 1;
    public Dictionary<string, InstallationVersionMetadata> Versions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Metadata for a specific installed version.
/// </summary>
public sealed class InstallationVersionMetadata
{
    public string Version { get; init; } = string.Empty;
    public DateTimeOffset InstalledAt { get; init; } = DateTimeOffset.UtcNow;
    public string CommitSha { get; init; } = "unknown";
    public Dictionary<string, ComponentFileMetadata> Components { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Metadata for a single component artifact (e.g. AddIn, Bridge, CLI).
/// </summary>
public sealed class ComponentFileMetadata
{
    public string RelativePath { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}
