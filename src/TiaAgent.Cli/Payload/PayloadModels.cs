using System;
using System.Collections.Generic;

namespace TiaAgent.Cli.Payload;

/// <summary>
/// Manifest schema for payload-manifest.json inside the CLI tool package.
/// </summary>
public sealed class PayloadManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string ProductVersion { get; init; } = string.Empty;
    public string CommitSha { get; init; } = "unknown";
    public DateTimeOffset BuiltAt { get; init; } = DateTimeOffset.UtcNow;
    public PayloadCompatibilityMetadata Compatibility { get; init; } = new();
    public Dictionary<string, PayloadComponentMetadata> Components { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PayloadFileEntry> Files { get; init; } = new();
}

/// <summary>
/// Target platform and compatibility declarations.
/// </summary>
public sealed class PayloadCompatibilityMetadata
{
    public string TiaPortalVersion { get; init; } = "V21";
    public string OpennessVersion { get; init; } = "V21";
    public string TargetFramework { get; init; } = "net8.0";
}

/// <summary>
/// Metadata for a key component in the payload (e.g. Bridge, AddIn).
/// </summary>
public sealed class PayloadComponentMetadata
{
    public string RelativePath { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

/// <summary>
/// Record of an individual file within the bundled payload.
/// </summary>
public sealed class PayloadFileEntry
{
    public string RelativePath { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

/// <summary>
/// Result of validating a bundled payload.
/// </summary>
public sealed class PayloadValidationResult
{
    public bool IsValid { get; init; }
    public PayloadManifest? Manifest { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static PayloadValidationResult Success(PayloadManifest manifest) =>
        new() { IsValid = true, Manifest = manifest };

    public static PayloadValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors };

    public static PayloadValidationResult Failure(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = new List<string>(errors) };
}
