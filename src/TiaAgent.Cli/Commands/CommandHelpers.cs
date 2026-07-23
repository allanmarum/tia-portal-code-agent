using System;
using System.IO;

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
    /// Logs each deployment to <paramref name="stdout"/>. Failures are logged as warnings and do not abort.
    /// </summary>
    internal static void DeployAddInIfPresent(string versionDir, string? customUserAddInsDir, TextWriter stdout)
    {
        if (!Directory.Exists(versionDir))
        {
            return;
        }

        var userAddInsDir = ResolveUserAddInsDir(customUserAddInsDir);

        var addinSubDir = Path.Combine(versionDir, "AddIn");
        if (!Directory.Exists(addinSubDir))
        {
            return;
        }

        var addinFiles = Directory.GetFiles(addinSubDir, "*.addin");
        if (addinFiles.Length == 0)
        {
            return;
        }

        Directory.CreateDirectory(userAddInsDir);
        foreach (var addinFile in addinFiles)
        {
            var destFile = Path.Combine(userAddInsDir, Path.GetFileName(addinFile));
            try
            {
                File.Copy(addinFile, destFile, overwrite: true);
                stdout.WriteLine($"Deployed Add-In artifact '{Path.GetFileName(addinFile)}' to '{userAddInsDir}'.");
            }
            catch (IOException ex)
            {
                stdout.WriteLine($"Warning: Failed to deploy Add-In artifact '{Path.GetFileName(addinFile)}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                stdout.WriteLine($"Warning: Failed to deploy Add-In artifact '{Path.GetFileName(addinFile)}': {ex.Message}");
            }
        }
    }
}
