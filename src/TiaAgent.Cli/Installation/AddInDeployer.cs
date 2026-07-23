using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaAgent.Cli.Installation;

/// <summary>
/// Deploys TIA Portal Add-In packages to the UserAddIns directory.
/// Consolidates deployment logic used by InstallCommand, ActivateCommand, UpdateCommand, and RollbackCommand.
/// </summary>
public static partial class AddInDeployer
{
    /// <summary>
    /// Pattern for matching TIA Agent Add-In filenames: TiaAgent-VERSION.addin
    /// </summary>
    private static readonly Regex s_addInVersionPattern = AddInVersionPattern();

    /// <summary>
    /// Deploys Add-In files from the version directory to the TIA Portal UserAddIns directory.
    /// Always preserves the Add-In locally as a fallback for manual installation.
    /// </summary>
    /// <param name="versionDir">The installed version directory containing an AddIn/ subdirectory.</param>
    /// <param name="customUserAddInsDir">Optional explicit UserAddIns directory override.</param>
    /// <param name="fallbackBaseDir">Base directory for local fallback preservation (typically the layout root).</param>
    /// <param name="stdout">Writer for deployment messages.</param>
    /// <returns>Deployment result with status and paths.</returns>
    public static AddInDeploymentResult Deploy(
        string versionDir,
        string? customUserAddInsDir,
        string fallbackBaseDir,
        TextWriter stdout)
    {
        // Find .addin files in the version directory
        var addInFiles = FindAddInFiles(versionDir);
        if (addInFiles.Count == 0)
        {
            return new AddInDeploymentResult
            {
                Status = AddInDeploymentStatus.NoAddInPackage,
                ErrorMessage = $"No .addin package found in '{versionDir}'."
            };
        }

        // Preserve locally as fallback (always, regardless of TIA Portal detection)
        string? fallbackDir = null;
        string? fallbackPath = null;
        try
        {
            fallbackDir = PreserveLocally(addInFiles[0], fallbackBaseDir, stdout);
            fallbackPath = Path.Combine(fallbackDir, Path.GetFileName(addInFiles[0]));
        }
        catch (Exception ex)
        {
            stdout.WriteLine($"Warning: Could not preserve Add-In locally: {ex.Message}");
        }

        // Discover TIA Portal V21
        var discovery = TiaPortalDiscovery.Discover(customUserAddInsDir);

        // Deploy to UserAddIns
        if (!discovery.UserAddInsDirectoryExists && !discovery.TiaPortalDetected)
        {
            stdout.WriteLine();
            stdout.WriteLine("TIA Portal V21 was not detected on this machine.");
            stdout.WriteLine($"Add-In preserved for manual installation at: {fallbackPath}");
            stdout.WriteLine($"To install manually: copy the .addin file to '{discovery.UserAddInsDirectory}'");
            stdout.WriteLine();

            return new AddInDeploymentResult
            {
                Status = AddInDeploymentStatus.FallbackOnly,
                FallbackDirectory = fallbackDir,
                FallbackAddInPath = fallbackPath,
                InstalledAddInVersion = ExtractVersion(addInFiles[0])
            };
        }

        if (!discovery.UserAddInsDirectoryExists && discovery.TiaPortalDetected)
        {
            stdout.WriteLine();
            stdout.WriteLine("TIA Portal V21 was detected but the UserAddIns directory does not exist.");
            stdout.WriteLine($"Add-In preserved for manual installation at: {fallbackPath}");
            stdout.WriteLine($"To install manually: create '{discovery.UserAddInsDirectory}' and copy the .addin file there.");
            stdout.WriteLine();

            return new AddInDeploymentResult
            {
                Status = AddInDeploymentStatus.UserAddInsDirMissing,
                FallbackDirectory = fallbackDir,
                FallbackAddInPath = fallbackPath,
                InstalledAddInVersion = ExtractVersion(addInFiles[0])
            };
        }

        // UserAddIns directory is available — deploy
        try
        {
            Directory.CreateDirectory(discovery.UserAddInsDirectory);
        }
        catch (Exception ex)
        {
            stdout.WriteLine();
            stdout.WriteLine($"Failed to create UserAddIns directory '{discovery.UserAddInsDirectory}': {ex.Message}");
            stdout.WriteLine($"Add-In preserved for manual installation at: {fallbackPath}");
            stdout.WriteLine();

            return new AddInDeploymentResult
            {
                Status = AddInDeploymentStatus.UserAddInsDirMissing,
                FallbackDirectory = fallbackDir,
                FallbackAddInPath = fallbackPath,
                ErrorMessage = ex.Message,
                InstalledAddInVersion = ExtractVersion(addInFiles[0])
            };
        }

        // Clean up stale Add-In versions before deploying
        var removedStale = RemoveStaleAddIns(discovery.UserAddInsDirectory, Path.GetFileName(addInFiles[0]), stdout);

        // Copy the new Add-In
        var destFile = Path.Combine(discovery.UserAddInsDirectory, Path.GetFileName(addInFiles[0]));
        try
        {
            File.Copy(addInFiles[0], destFile, overwrite: true);
        }
        catch (IOException ex)
        {
            stdout.WriteLine();
            stdout.WriteLine($"Failed to deploy Add-In to '{destFile}': {ex.Message}");
            stdout.WriteLine($"Add-In preserved for manual installation at: {fallbackPath}");
            stdout.WriteLine();

            return new AddInDeploymentResult
            {
                Status = AddInDeploymentStatus.Error,
                FallbackDirectory = fallbackDir,
                FallbackAddInPath = fallbackPath,
                ErrorMessage = ex.Message,
                InstalledAddInVersion = ExtractVersion(addInFiles[0])
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            stdout.WriteLine();
            stdout.WriteLine($"Permission denied deploying Add-In to '{destFile}': {ex.Message}");
            stdout.WriteLine($"Add-In preserved for manual installation at: {fallbackPath}");
            stdout.WriteLine();

            return new AddInDeploymentResult
            {
                Status = AddInDeploymentStatus.Error,
                FallbackDirectory = fallbackDir,
                FallbackAddInPath = fallbackPath,
                ErrorMessage = ex.Message,
                InstalledAddInVersion = ExtractVersion(addInFiles[0])
            };
        }

        var version = ExtractVersion(addInFiles[0]);
        stdout.WriteLine($"Deployed Add-In '{Path.GetFileName(addInFiles[0])}' to '{discovery.UserAddInsDirectory}'.");
        stdout.WriteLine($"Installed Add-In version: {version}");

        var status = fallbackDir != null
            ? AddInDeploymentStatus.DeployedWithFallback
            : AddInDeploymentStatus.Deployed;

        return new AddInDeploymentResult
        {
            Status = status,
            InstalledAddInPath = destFile,
            InstalledAddInVersion = version,
            FallbackDirectory = fallbackDir,
            FallbackAddInPath = fallbackPath,
            RemovedStaleFiles = removedStale
        };
    }

    /// <summary>
    /// Finds all .addin files in the version's AddIn subdirectory.
    /// </summary>
    public static IReadOnlyList<string> FindAddInFiles(string versionDir)
    {
        var addinSubDir = Path.Combine(versionDir, "AddIn");
        if (!Directory.Exists(addinSubDir))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(addinSubDir, "*.addin");
    }

    /// <summary>
    /// Removes stale Add-In files from the UserAddIns directory, keeping only the current version.
    /// </summary>
    public static IReadOnlyList<string> RemoveStaleAddIns(
        string userAddInsDir,
        string currentAddInFileName,
        TextWriter stdout)
    {
        if (!Directory.Exists(userAddInsDir))
        {
            return Array.Empty<string>();
        }

        var removed = new List<string>();
        var candidates = Directory.GetFiles(userAddInsDir, "*.addin");

        foreach (var file in candidates)
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, currentAddInFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Only remove files that match the TiaAgent pattern
            if (!s_addInVersionPattern.IsMatch(fileName))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                removed.Add(fileName);
                stdout.WriteLine($"Removed stale Add-In: {fileName}");
            }
            catch (Exception ex)
            {
                stdout.WriteLine($"Warning: Failed to remove stale Add-In '{fileName}': {ex.Message}");
            }
        }

        return removed;
    }

    /// <summary>
    /// Preserves the Add-In file locally for manual installation.
    /// Returns the fallback directory path.
    /// </summary>
    public static string PreserveLocally(string addinSourcePath, string fallbackBaseDir, TextWriter stdout)
    {
        var fallbackDir = Path.Combine(fallbackBaseDir, "AddIn");
        Directory.CreateDirectory(fallbackDir);

        var destFile = Path.Combine(fallbackDir, Path.GetFileName(addinSourcePath));
        File.Copy(addinSourcePath, destFile, overwrite: true);

        return fallbackDir;
    }

    /// <summary>
    /// Extracts the version string from an Add-In filename.
    /// E.g., "TiaAgent-0.2.0.addin" → "0.2.0"
    /// </summary>
    public static string? ExtractVersion(string addinFilePath)
    {
        var fileName = Path.GetFileName(addinFilePath);
        var match = s_addInVersionPattern.Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^TiaAgent-(.+)\.addin$", RegexOptions.IgnoreCase)]
    private static partial Regex AddInVersionPattern();
}
