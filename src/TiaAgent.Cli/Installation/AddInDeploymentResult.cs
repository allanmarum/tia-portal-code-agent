using System.Collections.Generic;

namespace TiaAgent.Cli.Installation;

/// <summary>
/// Status of Add-In deployment.
/// </summary>
public enum AddInDeploymentStatus
{
    /// <summary>Add-In was successfully deployed to the UserAddIns directory.</summary>
    Deployed,

    /// <summary>Add-In was deployed and also preserved locally as a fallback.</summary>
    DeployedWithFallback,

    /// <summary>TIA Portal not detected; Add-In preserved locally for manual installation.</summary>
    FallbackOnly,

    /// <summary>TIA Portal detected but UserAddIns directory could not be created.</summary>
    UserAddInsDirMissing,

    /// <summary>No .addin package file was found in the payload.</summary>
    NoAddInPackage,

    /// <summary>An unexpected error occurred during deployment.</summary>
    Error
}

/// <summary>
/// Result of an Add-In deployment attempt.
/// </summary>
public sealed class AddInDeploymentResult
{
    /// <summary>The deployment status.</summary>
    public AddInDeploymentStatus Status { get; init; }

    /// <summary>Full path where the .addin was deployed (in UserAddIns), if deployed.</summary>
    public string? InstalledAddInPath { get; init; }

    /// <summary>The Add-In version extracted from the filename (e.g., "0.2.0" from "TiaAgent-0.2.0.addin").</summary>
    public string? InstalledAddInVersion { get; init; }

    /// <summary>Local fallback directory where the .addin was preserved for manual installation.</summary>
    public string? FallbackDirectory { get; init; }

    /// <summary>Full path to the preserved .addin file in the fallback directory.</summary>
    public string? FallbackAddInPath { get; init; }

    /// <summary>Error message, if Status is Error.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Stale .addin files that were removed during deployment.</summary>
    public IReadOnlyList<string> RemovedStaleFiles { get; init; } = System.Array.Empty<string>();

    /// <summary>Whether the deployment was fully successful (Add-In is in UserAddIns).</summary>
    public bool IsFullyDeployed => Status is AddInDeploymentStatus.Deployed or AddInDeploymentStatus.DeployedWithFallback;

    /// <summary>Whether the Add-In is available for manual installation (either deployed or preserved locally).</summary>
    public bool IsAvailable => Status is not AddInDeploymentStatus.NoAddInPackage and not AddInDeploymentStatus.Error;
}
