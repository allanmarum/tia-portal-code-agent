using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TiaAgent.Cli.Release;

/// <summary>
/// Validates release distribution integrity, checksums, SBOM, release manifest, and component boundaries.
/// </summary>
public static class ReleaseValidator
{
    public static ReleaseValidationResult ValidateRelease(string releaseDirectory, string? expectedVersion = null)
    {
        if (string.IsNullOrWhiteSpace(releaseDirectory))
        {
            return ReleaseValidationResult.Failure("Release directory path cannot be null or empty.");
        }

        if (!Directory.Exists(releaseDirectory))
        {
            return ReleaseValidationResult.Failure($"Release directory does not exist: '{releaseDirectory}'");
        }

        var errors = new List<string>();

        // 1. Validate release-manifest.json presence & content
        var manifestPath = Path.Combine(releaseDirectory, ReleaseStore.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            errors.Add($"Missing required release manifest file: '{ReleaseStore.ManifestFileName}'.");
            return ReleaseValidationResult.Failure(errors);
        }

        ReleaseManifest manifest;
        try
        {
            manifest = ReleaseStore.ReadManifest(releaseDirectory);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to read release manifest: {ex.Message}");
            return ReleaseValidationResult.Failure(errors);
        }

        if (manifest.SchemaVersion != 1)
        {
            errors.Add($"Unsupported release manifest schemaVersion '{manifest.SchemaVersion}'. Expected 1.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ProductVersion))
        {
            errors.Add("Release manifest productVersion is empty.");
        }

        if (!string.IsNullOrWhiteSpace(expectedVersion) &&
            !string.Equals(manifest.ProductVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Release product version mismatch: manifest has '{manifest.ProductVersion}', expected '{expectedVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Channel))
        {
            errors.Add("Release manifest channel is empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CommitSha))
        {
            errors.Add("Release manifest commitSha is empty.");
        }

        if (manifest.Compatibility == null ||
            string.IsNullOrWhiteSpace(manifest.Compatibility.TiaPortalVersion))
        {
            errors.Add("Release manifest compatibility metadata is missing or incomplete.");
        }

        // 2. Validate THIRD_PARTY_NOTICES.md
        var noticesPath = Path.Combine(releaseDirectory, ReleaseStore.NoticesFileName);
        if (!File.Exists(noticesPath))
        {
            errors.Add($"Missing required release file: '{ReleaseStore.NoticesFileName}'.");
        }

        // 3. Validate sbom.spdx.json
        var sbomPath = Path.Combine(releaseDirectory, ReleaseStore.SbomFileName);
        if (!File.Exists(sbomPath))
        {
            errors.Add($"Missing required SBOM file: '{ReleaseStore.SbomFileName}'.");
        }
        else
        {
            try
            {
                var sbomText = File.ReadAllText(sbomPath);
                using var doc = JsonDocument.Parse(sbomText);
                if (!doc.RootElement.TryGetProperty("spdxVersion", out var spdxVersion) ||
                    !spdxVersion.GetString()!.StartsWith("SPDX"))
                {
                    errors.Add("SBOM file does not contain valid SPDX JSON.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid SBOM JSON format: {ex.Message}");
            }
        }

        // 4. Validate SHA256SUMS
        var checksumsPath = Path.Combine(releaseDirectory, ReleaseStore.ChecksumsFileName);
        if (!File.Exists(checksumsPath))
        {
            errors.Add($"Missing required checksums file: '{ReleaseStore.ChecksumsFileName}'.");
        }
        else
        {
            try
            {
                var fileHashes = ReleaseStore.ReadChecksumsFile(checksumsPath);
                if (fileHashes.Count == 0)
                {
                    errors.Add("Checksums file 'SHA256SUMS' is empty.");
                }

                foreach (var (fileName, expectedHash) in fileHashes)
                {
                    var filePath = Path.Combine(releaseDirectory, fileName);
                    if (!File.Exists(filePath))
                    {
                        errors.Add($"File listed in SHA256SUMS missing from release directory: '{fileName}'.");
                        continue;
                    }

                    var actualHash = ReleaseStore.ComputeSha256(filePath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"SHA256 hash mismatch in SHA256SUMS for '{fileName}': expected '{expectedHash}', computed '{actualHash}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse or verify SHA256SUMS: {ex.Message}");
            }
        }

        // 5. Validate Artifacts array in release-manifest.json
        if (manifest.Artifacts == null || manifest.Artifacts.Count == 0)
        {
            errors.Add("Release manifest does not list any artifact entries.");
        }
        else
        {
            foreach (var artifact in manifest.Artifacts)
            {
                if (string.IsNullOrWhiteSpace(artifact.Name))
                {
                    errors.Add("Release manifest contains an artifact entry with an empty name.");
                    continue;
                }

                var fullPath = Path.Combine(releaseDirectory, artifact.Name);
                if (!File.Exists(fullPath))
                {
                    errors.Add($"Artifact listed in release manifest missing from directory: '{artifact.Name}'.");
                    continue;
                }

                if (string.Equals(artifact.Name, ReleaseStore.ChecksumsFileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(artifact.Name, ReleaseStore.ManifestFileName, StringComparison.OrdinalIgnoreCase))
                {
                    // Manifest and SHA256SUMS file existence verified; checksum verification for all files is handled by SHA256SUMS parser step.
                    continue;
                }

                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length != artifact.SizeBytes)
                {
                    errors.Add($"Artifact size mismatch for '{artifact.Name}': expected {artifact.SizeBytes} bytes, found {fileInfo.Length} bytes.");
                }

                if (!string.IsNullOrWhiteSpace(artifact.Sha256Hash))
                {
                    var actualHash = ReleaseStore.ComputeSha256(fullPath);
                    if (!string.Equals(actualHash, artifact.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Artifact SHA256 hash mismatch for '{artifact.Name}': expected '{artifact.Sha256Hash}', found '{actualHash}'.");
                    }
                }
            }
        }

        // 6. Verify prohibited Siemens runtime assemblies are not present
        var allFiles = Directory.EnumerateFiles(releaseDirectory, "*", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("Siemens.Engineering", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Siemens.Automation", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Prohibited Siemens runtime assembly found in release directory: '{fileName}'. Siemens binaries must remain external.");
            }
        }

        if (errors.Count > 0)
        {
            return ReleaseValidationResult.Failure(errors);
        }

        return ReleaseValidationResult.Success(manifest);
    }
}
