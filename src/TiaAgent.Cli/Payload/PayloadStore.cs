using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using TiaAgent.Cli.Layout;

namespace TiaAgent.Cli.Payload;

/// <summary>
/// Manages reading, writing, and computing metadata for payload-manifest.json.
/// </summary>
public static class PayloadStore
{
    public const string ManifestFileName = "payload-manifest.json";

    public static PayloadManifest ReadManifest(string payloadDirectory)
    {
        if (string.IsNullOrWhiteSpace(payloadDirectory))
        {
            throw new ArgumentException("Payload directory cannot be null or empty.", nameof(payloadDirectory));
        }

        var manifestPath = Path.Combine(payloadDirectory, ManifestFileName);
        return ManifestStore.Read<PayloadManifest>(manifestPath);
    }

    public static void WriteManifest(string payloadDirectory, PayloadManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(payloadDirectory))
        {
            throw new ArgumentException("Payload directory cannot be null or empty.", nameof(payloadDirectory));
        }

        ArgumentNullException.ThrowIfNull(manifest);

        var manifestPath = Path.Combine(payloadDirectory, ManifestFileName);
        ManifestStore.WriteAtomic(manifestPath, manifest);
    }

    public static string ComputeSha256(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found for hash calculation: {filePath}", filePath);
        }

        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
