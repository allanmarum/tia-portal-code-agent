using System;
using System.IO;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Shared utilities used by multiple runtime adapters.
/// </summary>
internal static class RuntimeHelpers
{
    /// <summary>
    /// Escapes a string for safe use as a shell argument.
    /// Wraps in double quotes and escapes special characters for Windows cmd.
    /// </summary>
    internal static string EscapeShellArg(string arg)
    {
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    /// <summary>
    /// Returns the full path if the file is found on PATH, null otherwise.
    /// </summary>
    internal static string? FindOnPath(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
