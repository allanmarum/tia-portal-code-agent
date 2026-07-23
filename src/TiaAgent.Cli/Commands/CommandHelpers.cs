using System;
using System.IO;
using TiaAgent.Cli.Installation;

namespace TiaAgent.Cli.Commands;

/// <summary>
/// Shared helpers for CLI commands.
/// </summary>
internal static class CommandHelpers
{
    /// <summary>
    /// Default Siemens TIA Portal UserAddIns directory name under %APPDATA%.
    /// </summary>
    private const string DefaultUserAddInsRelativePath = "Siemens/Automation/Portal V21/UserAddIns";

    /// <summary>
    /// Resolves the UserAddIns directory: uses <paramref name="customUserAddInsDir"/> if provided,
    /// otherwise falls back to %APPDATA%/Siemens/Automation/Portal V21/UserAddIns.
    /// </summary>
    internal static string ResolveUserAddInsDir(string? customUserAddInsDir)
    {
        if (!string.IsNullOrWhiteSpace(customUserAddInsDir))
        {
            return customUserAddInsDir;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, DefaultUserAddInsRelativePath);
    }

    /// <summary>
    /// Deploys .addin files from <paramref name="versionDir"/>/AddIn to the Siemens UserAddIns directory.
    /// Uses the shared <see cref="AddInDeployer"/> service for consistent deployment behavior.
    /// Logs each deployment to <paramref name="stdout"/>. Failures are logged as warnings and do not abort.
    /// </summary>
    internal static void DeployAddInIfPresent(string versionDir, string? customUserAddInsDir, TextWriter stdout)
    {
        if (!Directory.Exists(versionDir))
        {
            return;
        }

        // Derive fallback base directory from versionDir (versions/VERSION → versions → root)
        var fallbackBaseDir = Path.GetDirectoryName(Path.GetDirectoryName(versionDir))
            ?? Path.GetTempPath();

        var result = AddInDeployer.Deploy(versionDir, customUserAddInsDir, fallbackBaseDir, stdout);

        if (result.Status == AddInDeploymentStatus.NoAddInPackage)
        {
            // Silent — no Add-In to deploy is expected for dev builds
            return;
        }

        if (result.Status == AddInDeploymentStatus.Error)
        {
            stdout.WriteLine($"Warning: Add-In deployment encountered an error: {result.ErrorMessage}");
        }
    }
}
