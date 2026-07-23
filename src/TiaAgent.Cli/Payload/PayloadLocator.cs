using System;
using System.IO;

namespace TiaAgent.Cli.Payload;

/// <summary>
/// Locates the bundled payload directory within the installed CLI package or base directory.
/// </summary>
public static class PayloadLocator
{
    /// <summary>
    /// Returns the absolute path to the payload directory.
    /// Checks custom path, then AppContext.BaseDirectory/payload, and falls back to AppContext.BaseDirectory.
    /// </summary>
    public static string GetBundledPayloadDirectory(string? customBasePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customBasePath))
        {
            var customPayload = Path.Combine(customBasePath, "payload");
            if (Directory.Exists(customPayload))
            {
                return customPayload;
            }
            return customBasePath;
        }

        var baseDir = AppContext.BaseDirectory;
        var subDir = Path.Combine(baseDir, "payload");
        if (Directory.Exists(subDir))
        {
            return subDir;
        }

        return baseDir;
    }
}
