using System;
using System.IO;

namespace TiaAgent.Cli.Installation;

/// <summary>
/// Result of TIA Portal V21 installation discovery.
/// </summary>
public sealed class TiaPortalDiscoveryResult
{
    /// <summary>Whether TIA Portal V21 installation was detected.</summary>
    public bool TiaPortalDetected { get; init; }

    /// <summary>Whether the UserAddIns directory exists.</summary>
    public bool UserAddInsDirectoryExists { get; init; }

    /// <summary>Resolved path to the UserAddIns directory (may not exist yet).</summary>
    public string UserAddInsDirectory { get; init; } = string.Empty;

    /// <summary>Detected TIA Portal V21 installation root, if found.</summary>
    public string? TiaPortalInstallPath { get; init; }

    /// <summary>How the path was resolved.</summary>
    public string DetectionSource { get; init; } = "not-detected";
}

/// <summary>
/// Discovers TIA Portal V21 installation paths on the current machine.
///
/// Detection order:
///   1. Explicit CLI override (--user-addins-dir)
///   2. TiaPublicApiDir environment variable
///   3. Standard installation path: C:\Program Files\Siemens\Automation\Portal V21
///   4. UserAddIns directory under %APPDATA% (residual from previous installation)
///
/// Note: The UserAddIns directory is always under %APPDATA%, NOT under the TIA Portal
/// installation directory. The TIA Portal installation is detected separately.
/// </summary>
public static class TiaPortalDiscovery
{
    private const string DefaultTiaPortalRoot = @"C:\Program Files\Siemens\Automation\Portal V21";
    private const string UserAddInsRelativePath = @"Siemens\Automation\Portal V21\UserAddIns";

    /// <summary>
    /// Discovers TIA Portal V21 installation and resolves the UserAddIns directory.
    /// </summary>
    public static TiaPortalDiscoveryResult Discover(string? customUserAddInsDir = null)
    {
        // 1. Explicit CLI override — highest priority
        if (!string.IsNullOrWhiteSpace(customUserAddInsDir))
        {
            var dirExists = Directory.Exists(customUserAddInsDir);
            return new TiaPortalDiscoveryResult
            {
                TiaPortalDetected = true,
                UserAddInsDirectoryExists = dirExists,
                UserAddInsDirectory = customUserAddInsDir,
                TiaPortalInstallPath = null,
                DetectionSource = "cli-override"
            };
        }

        var appDataUserAddIns = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            UserAddInsRelativePath);

        // 2. Check TiaPublicApiDir environment variable
        var envApiDir = Environment.GetEnvironmentVariable("TiaPublicApiDir");
        if (!string.IsNullOrWhiteSpace(envApiDir) && Directory.Exists(envApiDir))
        {
            var tiaRoot = DeriveTiaRootFromApiDir(envApiDir);
            var dirExists = Directory.Exists(appDataUserAddIns);

            return new TiaPortalDiscoveryResult
            {
                TiaPortalDetected = true,
                UserAddInsDirectoryExists = dirExists,
                UserAddInsDirectory = appDataUserAddIns,
                TiaPortalInstallPath = tiaRoot,
                DetectionSource = "env-var"
            };
        }

        // 3. Check standard installation path
        if (Directory.Exists(DefaultTiaPortalRoot))
        {
            var dirExists = Directory.Exists(appDataUserAddIns);

            return new TiaPortalDiscoveryResult
            {
                TiaPortalDetected = true,
                UserAddInsDirectoryExists = dirExists,
                UserAddInsDirectory = appDataUserAddIns,
                TiaPortalInstallPath = DefaultTiaPortalRoot,
                DetectionSource = "standard-path"
            };
        }

        // 4. Check if UserAddIns directory exists under %APPDATA% (TIA Portal may have been
        //    installed previously but the Program Files directory was removed)
        if (Directory.Exists(appDataUserAddIns))
        {
            return new TiaPortalDiscoveryResult
            {
                TiaPortalDetected = true,
                UserAddInsDirectoryExists = true,
                UserAddInsDirectory = appDataUserAddIns,
                TiaPortalInstallPath = null,
                DetectionSource = "appdata-legacy"
            };
        }

        // 5. Not detected
        return new TiaPortalDiscoveryResult
        {
            TiaPortalDetected = false,
            UserAddInsDirectoryExists = false,
            UserAddInsDirectory = appDataUserAddIns,
            TiaPortalInstallPath = null,
            DetectionSource = "not-detected"
        };
    }

    /// <summary>
    /// Derives the TIA Portal root from a PublicAPI directory path.
    /// E.g., "C:\...\Portal V21\PublicAPI\V21\net48" → "C:\...\Portal V21"
    ///
    /// Path structure: TIA_Root/PublicAPI/V21/net48
    /// net48 → V21 (1) → PublicAPI (2) → TIA_Root (3)
    /// </summary>
    public static string DeriveTiaRootFromApiDir(string apiDir)
    {
        var dir = new DirectoryInfo(apiDir);
        for (int i = 0; i < 3 && dir != null; i++)
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? DefaultTiaPortalRoot;
    }
}
