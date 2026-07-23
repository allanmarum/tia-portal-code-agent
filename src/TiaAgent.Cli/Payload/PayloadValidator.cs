using System;
using System.Collections.Generic;
using System.IO;

namespace TiaAgent.Cli.Payload;

/// <summary>
/// Validates the integrity, version alignment, component hashes, and licensing boundaries of a bundled CLI payload.
/// </summary>
public static class PayloadValidator
{
    public static PayloadValidationResult ValidatePayload(string payloadDirectory, string? expectedVersion = null)
    {
        if (string.IsNullOrWhiteSpace(payloadDirectory))
        {
            return PayloadValidationResult.Failure("Payload directory path cannot be null or empty.");
        }

        if (!Directory.Exists(payloadDirectory))
        {
            return PayloadValidationResult.Failure($"Payload directory does not exist: {payloadDirectory}");
        }

        var manifestPath = Path.Combine(payloadDirectory, PayloadStore.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return PayloadValidationResult.Failure($"Payload manifest file missing: {manifestPath}");
        }

        PayloadManifest manifest;
        try
        {
            manifest = PayloadStore.ReadManifest(payloadDirectory);
        }
        catch (Exception ex)
        {
            return PayloadValidationResult.Failure($"Failed to read payload manifest: {ex.Message}");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.ProductVersion))
        {
            errors.Add("Payload manifest productVersion is empty.");
        }

        if (!string.IsNullOrWhiteSpace(expectedVersion) &&
            !string.Equals(manifest.ProductVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Payload product version mismatch: manifest has '{manifest.ProductVersion}', expected '{expectedVersion}'.");
        }

        // Validate component versions if specified
        foreach (var (compName, compMeta) in manifest.Components)
        {
            if (string.IsNullOrWhiteSpace(compMeta.Version))
            {
                errors.Add($"Component '{compName}' version declaration is empty.");
            }
        }

        // Verify prohibited Siemens runtime assemblies are not included in payload
        var allFiles = Directory.EnumerateFiles(payloadDirectory, "*", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("Siemens.Engineering", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Siemens.Automation", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Prohibited Siemens runtime assembly found in payload: '{fileName}'. Siemens binaries must remain external.");
            }
        }

        // Verify each registered payload file
        if (manifest.Files.Count == 0)
        {
            errors.Add("Payload manifest does not contain any file entries.");
        }

        foreach (var fileEntry in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(fileEntry.RelativePath))
            {
                errors.Add("Payload manifest contains a file entry with an empty relative path.");
                continue;
            }

            var fullPath = Path.Combine(payloadDirectory, fileEntry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                errors.Add($"Missing payload file: '{fileEntry.RelativePath}'.");
                continue;
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length != fileEntry.SizeBytes)
            {
                errors.Add($"File size mismatch for '{fileEntry.RelativePath}': expected {fileEntry.SizeBytes} bytes, found {fileInfo.Length} bytes.");
            }

            if (!string.IsNullOrWhiteSpace(fileEntry.Sha256Hash))
            {
                var actualHash = PayloadStore.ComputeSha256(fullPath);
                if (!string.Equals(actualHash, fileEntry.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"SHA256 hash mismatch for '{fileEntry.RelativePath}': expected {fileEntry.Sha256Hash}, found {actualHash}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return PayloadValidationResult.Failure(errors);
        }

        return PayloadValidationResult.Success(manifest);
    }
}
