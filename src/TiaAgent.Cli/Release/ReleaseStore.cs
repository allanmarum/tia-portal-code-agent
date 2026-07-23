using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiaAgent.Cli.Release;

/// <summary>
/// Persistence and checksum utilities for release metadata artifacts (release-manifest.json, SHA256SUMS).
/// </summary>
public static class ReleaseStore
{
    public const string ManifestFileName = "release-manifest.json";
    public const string ChecksumsFileName = "SHA256SUMS";
    public const string SbomFileName = "sbom.spdx.json";
    public const string NoticesFileName = "THIRD_PARTY_NOTICES.md";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly char[] s_splitChars = new[] { ' ', '\t' };

    public static ReleaseManifest ReadManifest(string directory)
    {
        var filePath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Release manifest file not found: '{filePath}'");
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<ReleaseManifest>(json, s_jsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize release manifest from '{filePath}'.");
    }

    public static void WriteManifest(string directory, ReleaseManifest manifest)
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, s_jsonOptions);
        File.WriteAllText(filePath, json + "\n", Encoding.UTF8);
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static void WriteChecksumsFile(string directory, IDictionary<string, string> fileHashes)
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, ChecksumsFileName);
        var lines = fileHashes
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => $"{kvp.Value.ToLowerInvariant()}  {kvp.Key}");

        File.WriteAllText(filePath, string.Join("\n", lines) + "\n", Encoding.UTF8);
    }

    public static Dictionary<string, string> ReadChecksumsFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Checksums file not found: '{filePath}'");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            // Format: <hash>  <filename> or <hash> *<filename>
            var parts = line.Split(s_splitChars, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var hash = parts[0].Trim().ToLowerInvariant();
                var fileName = parts[1].TrimStart('*').Trim();
                result[fileName] = hash;
            }
        }

        return result;
    }

    public static string ResolveChannel(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "dev";
        if (version.EndsWith("-dev", StringComparison.OrdinalIgnoreCase)) return "dev";
        if (version.Contains("-alpha", StringComparison.OrdinalIgnoreCase)) return "alpha";
        if (version.Contains("-beta", StringComparison.OrdinalIgnoreCase)) return "beta";
        if (version.Contains("-rc", StringComparison.OrdinalIgnoreCase)) return "rc";
        return "stable";
    }
}
