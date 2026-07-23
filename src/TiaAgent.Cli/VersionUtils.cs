using System;
using System.Text.RegularExpressions;

namespace TiaAgent.Cli;

/// <summary>
/// Helper utilities for Semantic Version comparison and parsing.
/// </summary>
public static class VersionUtils
{
    /// <summary>
    /// Compares two Semantic Version strings (e.g. "0.2.0-beta.1", "0.2.0", "1.0.0").
    /// Returns -1 if v1 &lt; v2, 0 if v1 == v2, 1 if v1 &gt; v2.
    /// </summary>
    public static int Compare(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
        if (string.IsNullOrWhiteSpace(v1)) return -1;
        if (string.IsNullOrWhiteSpace(v2)) return 1;

        if (string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase)) return 0;

        ParseSemVer(v1, out var core1, out var pre1);
        ParseSemVer(v2, out var core2, out var pre2);

        int coreCompare = core1.CompareTo(core2);
        if (coreCompare != 0)
        {
            return coreCompare;
        }

        // Core versions are equal. A version without a prerelease tag is greater than one with a prerelease tag.
        if (string.IsNullOrEmpty(pre1) && !string.IsNullOrEmpty(pre2)) return 1;
        if (!string.IsNullOrEmpty(pre1) && string.IsNullOrEmpty(pre2)) return -1;
        if (string.IsNullOrEmpty(pre1) && string.IsNullOrEmpty(pre2)) return 0;

        return ComparePrerelease(pre1, pre2);
    }

    private static void ParseSemVer(string versionStr, out Version coreVersion, out string prerelease)
    {
        var cleaned = versionStr.TrimStart('v', 'V');
        var dashIdx = cleaned.IndexOf('-');
        string corePart = dashIdx >= 0 ? cleaned.Substring(0, dashIdx) : cleaned;
        prerelease = dashIdx >= 0 ? cleaned.Substring(dashIdx + 1) : string.Empty;

        // Strip any build metadata suffix (+sha...)
        var plusIdx = prerelease.IndexOf('+');
        if (plusIdx >= 0)
        {
            prerelease = prerelease.Substring(0, plusIdx);
        }
        else
        {
            var corePlusIdx = corePart.IndexOf('+');
            if (corePlusIdx >= 0)
            {
                corePart = corePart.Substring(0, corePlusIdx);
            }
        }

        if (!Version.TryParse(corePart, out coreVersion!))
        {
            // Fallback for partial versions like "1" or "1.2"
            var parts = corePart.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out var ma) ? ma : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out var bu) ? bu : 0;
            int rev = parts.Length > 3 && int.TryParse(parts[3], out var re) ? re : 0;
            coreVersion = new Version(major, minor, build, rev);
        }
    }

    private static int ComparePrerelease(string pre1, string pre2)
    {
        var parts1 = pre1.Split('.');
        var parts2 = pre2.Split('.');

        int minLen = Math.Min(parts1.Length, parts2.Length);
        for (int i = 0; i < minLen; i++)
        {
            var p1 = parts1[i];
            var p2 = parts2[i];

            bool isNum1 = int.TryParse(p1, out var num1);
            bool isNum2 = int.TryParse(p2, out var num2);

            if (isNum1 && isNum2)
            {
                int cmp = num1.CompareTo(num2);
                if (cmp != 0) return cmp;
            }
            else if (isNum1)
            {
                return -1; // Numeric identifiers have lower precedence than text
            }
            else if (isNum2)
            {
                return 1;
            }
            else
            {
                int cmp = string.Compare(p1, p2, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }

        return parts1.Length.CompareTo(parts2.Length);
    }
}
