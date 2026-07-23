using System;
using System.Collections.Generic;
using System.Linq;
using TiaAgent.Cli.Release;

namespace TiaAgent.Cli;

/// <summary>
/// Utilities for update channel management: channel validation, version filtering,
/// and channel transition safety checks.
/// </summary>
public static class ChannelUtils
{
    /// <summary>
    /// Supported channel names in order from most experimental to most stable.
    /// </summary>
    public static readonly string[] ValidChannels = { "alpha", "beta", "rc", "stable" };

    /// <summary>
    /// Maps channel names to their numeric precedence (higher = more stable).
    /// Used for channel transition validation.
    /// </summary>
    private static readonly Dictionary<string, int> ChannelPrecedence = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alpha"] = 0,
        ["beta"] = 1,
        ["rc"] = 2,
        ["stable"] = 3
    };

    /// <summary>
    /// Returns true if the given string is a valid channel name.
    /// </summary>
    public static bool IsValidChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return false;
        return ChannelPrecedence.ContainsKey(channel);
    }

    /// <summary>
    /// Normalizes a channel name to lowercase, or returns null if invalid.
    /// </summary>
    public static string? NormalizeChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;
        var lower = channel.Trim().ToLowerInvariant();
        return ChannelPrecedence.ContainsKey(lower) ? lower : null;
    }

    /// <summary>
    /// Returns true if the given version is compatible with the specified channel.
    /// - stable: only versions with no prerelease suffix
    /// - rc: rc and stable versions
    /// - beta: beta, rc, and stable versions
    /// - alpha: all versions (alpha, beta, rc, stable)
    /// </summary>
    public static bool IsVersionCompatibleWithChannel(string version, string channel)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        if (!IsValidChannel(channel)) return false;

        var versionChannel = ReleaseStore.ResolveChannel(version);

        // "dev" versions are never eligible for any user-facing channel
        if (string.Equals(versionChannel, "dev", StringComparison.OrdinalIgnoreCase))
            return false;

        return IsChannelIncluded(versionChannel, channel);
    }

    /// <summary>
    /// Returns true if a version's channel is included in the target channel's scope.
    /// E.g. "beta" is included in "alpha" (alpha includes everything), but not in "stable".
    /// </summary>
    public static bool IsChannelIncluded(string versionChannel, string targetChannel)
    {
        if (!ChannelPrecedence.TryGetValue(versionChannel, out var versionPrec)) return false;
        if (!ChannelPrecedence.TryGetValue(targetChannel, out var targetPrec)) return false;

        // A version is compatible if its channel precedence is >= the target's minimum
        // alpha(0) includes all, stable(3) includes only stable
        return versionPrec >= targetPrec;
    }

    /// <summary>
    /// Filters a list of version strings to those compatible with the given channel,
    /// and returns the highest version by SemVer precedence, or null if none match.
    /// </summary>
    public static string? ResolveBestVersion(IEnumerable<string> versions, string channel)
    {
        var compatible = versions
            .Where(v => IsVersionCompatibleWithChannel(v, channel))
            .ToList();

        if (compatible.Count == 0) return null;

        // Sort descending by SemVer and return the best
        compatible.Sort((a, b) => VersionUtils.Compare(b, a));
        return compatible[0];
    }

    /// <summary>
    /// Returns true if changing from one channel to another constitutes a downgrade
    /// (moving from a more stable channel to a less stable one).
    /// E.g. stable -> rc is a downgrade, alpha -> stable is an upgrade.
    /// </summary>
    public static bool IsChannelDowngrade(string fromChannel, string toChannel)
    {
        if (!ChannelPrecedence.TryGetValue(fromChannel, out var fromPrec)) return false;
        if (!ChannelPrecedence.TryGetValue(toChannel, out var toPrec)) return false;

        // Downgrade = moving to a lower precedence (less stable) channel
        return toPrec < fromPrec;
    }

    /// <summary>
    /// Returns the numeric precedence for a channel (higher = more stable).
    /// Returns -1 for unknown channels.
    /// </summary>
    public static int GetChannelPrecedence(string channel)
    {
        return ChannelPrecedence.TryGetValue(channel, out var prec) ? prec : -1;
    }
}
