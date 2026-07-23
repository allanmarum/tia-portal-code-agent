using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TiaAgent.Contracts.Runtime;

/// <summary>
/// Metadata describing supported agent runtimes, minimum/tested versions, and compatibility requirements.
/// </summary>
public sealed class RuntimeInfoMetadata
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MinimumVersion { get; set; } = "0.1.0";
    public string TestedVersion { get; set; } = "1.0.0";
    public string DefaultMode { get; set; } = "cli";
    public List<string> SupportedModes { get; set; } = new List<string>();
}

/// <summary>
/// Central registry of compatibility metadata and version validation for TIA Agent runtimes.
/// </summary>
public static class RuntimeCompatibilityRegistry
{
    private static readonly Regex s_versionRegex = new Regex(@"(?:v)?(?<ver>\d+\.\d+(?:\.\d+)?(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static readonly Dictionary<string, RuntimeInfoMetadata> KnownRuntimes = new Dictionary<string, RuntimeInfoMetadata>(StringComparer.OrdinalIgnoreCase)
    {
        ["opencode"] = new RuntimeInfoMetadata
        {
            Id = "opencode",
            DisplayName = "OpenCode",
            MinimumVersion = "0.1.0",
            TestedVersion = "1.0.0",
            DefaultMode = "server",
            SupportedModes = new List<string> { "server", "cli" }
        },
        ["mimo"] = new RuntimeInfoMetadata
        {
            Id = "mimo",
            DisplayName = "Mimo CLI",
            MinimumVersion = "0.1.0",
            TestedVersion = "1.0.0",
            DefaultMode = "cli",
            SupportedModes = new List<string> { "cli" }
        },
        ["claude"] = new RuntimeInfoMetadata
        {
            Id = "claude",
            DisplayName = "Claude Code CLI",
            MinimumVersion = "0.1.0",
            TestedVersion = "1.0.0",
            DefaultMode = "cli",
            SupportedModes = new List<string> { "cli" }
        }
    };

    public static bool IsKnownRuntime(string? runtimeId)
    {
        if (runtimeId == null) return false;
        var trimmed = runtimeId.Trim();
        if (trimmed.Length == 0) return false;
        return KnownRuntimes.ContainsKey(trimmed);
    }

    public static RuntimeInfoMetadata? GetMetadata(string? runtimeId)
    {
        if (runtimeId == null) return null;
        var trimmed = runtimeId.Trim();
        if (trimmed.Length == 0) return null;
        return KnownRuntimes.TryGetValue(trimmed, out var meta) ? meta : null;
    }

    /// <summary>
    /// Extracts a numeric version string (X.Y or X.Y.Z) from raw command output.
    /// </summary>
    public static string? ExtractSemVer(string? rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;

        var match = s_versionRegex.Match(rawOutput);
        if (match.Success)
        {
            return match.Groups["ver"].Value;
        }

        return null;
    }

    /// <summary>
    /// Checks if a detected version meets or exceeds the specified minimum version.
    /// </summary>
    public static bool IsVersionSupported(string? rawVersionOutput, string minimumVersion, out string? parsedVersion)
    {
        parsedVersion = ExtractSemVer(rawVersionOutput);
        if (parsedVersion == null || string.IsNullOrWhiteSpace(minimumVersion))
        {
            return true;
        }

        if (Version.TryParse(NormalizeVersionString(parsedVersion), out var detected) &&
            Version.TryParse(NormalizeVersionString(minimumVersion), out var min))
        {
            return detected >= min;
        }

        return true;
    }

    private static string NormalizeVersionString(string ver)
    {
        var parts = ver.Split('.');
        if (parts.Length == 1) return $"{ver}.0.0";
        if (parts.Length == 2) return $"{ver}.0";
        return ver;
    }
}
