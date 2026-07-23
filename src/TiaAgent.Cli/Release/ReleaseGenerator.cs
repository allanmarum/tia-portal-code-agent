using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaAgent.Cli.Release;

/// <summary>
/// Orchestrates generation of release metadata artifacts (SBOM, release-manifest.json, SHA256SUMS).
/// </summary>
public static class ReleaseGenerator
{
    public static ReleaseManifest GenerateReleaseMetadata(
        string artifactsDirectory,
        string productVersion,
        string commitSha,
        string? repoRoot = null,
        DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(artifactsDirectory))
        {
            throw new ArgumentException("Artifacts directory cannot be null or empty.", nameof(artifactsDirectory));
        }

        Directory.CreateDirectory(artifactsDirectory);
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var channel = ReleaseStore.ResolveChannel(productVersion);

        // 1. Copy THIRD_PARTY_NOTICES.md if available
        EnsureThirdPartyNotices(artifactsDirectory, repoRoot);

        // 2. Generate SPDX SBOM
        SbomGenerator.WriteSbom(artifactsDirectory, productVersion, commitSha, ts);

        // 3. Scan artifacts to build manifest component entries
        var components = new Dictionary<string, ReleaseComponentMetadata>(StringComparer.OrdinalIgnoreCase);

        var addinFile = Directory.GetFiles(artifactsDirectory, "*.addin").FirstOrDefault();
        if (addinFile != null)
        {
            var addinName = Path.GetFileName(addinFile);
            components["addin"] = new ReleaseComponentMetadata
            {
                RelativePath = addinName,
                Version = productVersion,
                Sha256Hash = ReleaseStore.ComputeSha256(addinFile),
                SizeBytes = new FileInfo(addinFile).Length
            };
        }

        var nupkgFile = Directory.GetFiles(artifactsDirectory, "*.nupkg").FirstOrDefault();
        if (nupkgFile != null)
        {
            var nupkgName = Path.GetFileName(nupkgFile);
            components["cli"] = new ReleaseComponentMetadata
            {
                RelativePath = nupkgName,
                Version = productVersion,
                Sha256Hash = ReleaseStore.ComputeSha256(nupkgFile),
                SizeBytes = new FileInfo(nupkgFile).Length
            };
        }

        // 4. Build artifact entries for payload files
        var artifacts = BuildArtifactEntries(artifactsDirectory);

        // Add entries for release-manifest.json and SHA256SUMS
        artifacts.Add(new ReleaseArtifactEntry
        {
            Name = ReleaseStore.ManifestFileName,
            Sha256Hash = "",
            SizeBytes = 0,
            Component = "manifest"
        });

        artifacts.Add(new ReleaseArtifactEntry
        {
            Name = ReleaseStore.ChecksumsFileName,
            Sha256Hash = "",
            SizeBytes = 0,
            Component = "checksums"
        });

        // 5. Build release manifest
        var manifest = new ReleaseManifest
        {
            SchemaVersion = 1,
            ProductVersion = productVersion,
            Channel = channel,
            CommitSha = commitSha,
            PublishedAt = ts,
            Compatibility = new ReleaseCompatibilityMetadata
            {
                TiaPortalVersion = "V21",
                OpennessVersion = "V21",
                TargetFramework = "net8.0"
            },
            Components = components,
            Artifacts = artifacts
        };

        // Write release-manifest.json
        ReleaseStore.WriteManifest(artifactsDirectory, manifest);

        // 6. Compute SHA256SUMS for all files in artifacts/ (including release-manifest.json, except SHA256SUMS itself)
        var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(artifactsDirectory))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, ReleaseStore.ChecksumsFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            fileHashes[fileName] = ReleaseStore.ComputeSha256(file);
        }

        ReleaseStore.WriteChecksumsFile(artifactsDirectory, fileHashes);

        return manifest;
    }

    private static List<ReleaseArtifactEntry> BuildArtifactEntries(string artifactsDirectory)
    {
        var entries = new List<ReleaseArtifactEntry>();
        var files = Directory.GetFiles(artifactsDirectory).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, ReleaseStore.ManifestFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ReleaseStore.ChecksumsFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileInfo = new FileInfo(file);
            var hash = ReleaseStore.ComputeSha256(file);

            var component = fileName switch
            {
                _ when fileName.EndsWith(".addin", StringComparison.OrdinalIgnoreCase) => "addin",
                _ when fileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) => "cli",
                ReleaseStore.SbomFileName => "sbom",
                ReleaseStore.NoticesFileName => "notices",
                _ => "artifact"
            };

            entries.Add(new ReleaseArtifactEntry
            {
                Name = fileName,
                Sha256Hash = hash,
                SizeBytes = fileInfo.Length,
                Component = component
            });
        }

        return entries;
    }

    private static void EnsureThirdPartyNotices(string artifactsDirectory, string? repoRoot)
    {
        var targetFile = Path.Combine(artifactsDirectory, ReleaseStore.NoticesFileName);
        if (File.Exists(targetFile)) return;

        var searchPaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            searchPaths.Add(Path.Combine(repoRoot, ReleaseStore.NoticesFileName));
        }

        searchPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ReleaseStore.NoticesFileName));
        searchPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), ReleaseStore.NoticesFileName));

        foreach (var src in searchPaths)
        {
            if (File.Exists(src))
            {
                File.Copy(src, targetFile, overwrite: true);
                return;
            }
        }

        var defaultNotice = """
        # Third-Party Notices

        TIA Portal Code Agent is licensed under the MIT License.
        Refer to repository documentation for third-party component licenses.
        """;
        File.WriteAllText(targetFile, defaultNotice + "\n");
    }
}
