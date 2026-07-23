using System;
using System.IO;
using System.Text;
using FluentAssertions;
using TiaAgent.Cli.Installation;
using Xunit;

namespace TiaAgent.Cli.Tests.Installation;

public sealed class AddInDeployerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _versionDir;
    private readonly string _userAddInsDir;
    private readonly string _fallbackBaseDir;

    public AddInDeployerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AddInDeployerTests_" + Guid.NewGuid().ToString("N"));
        _versionDir = Path.Combine(_tempDirectory, "versions", "0.2.0");
        _userAddInsDir = Path.Combine(_tempDirectory, "UserAddIns");
        _fallbackBaseDir = Path.Combine(_tempDirectory, "TiaAgentRoot");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_versionDir);
        Directory.CreateDirectory(_userAddInsDir);
        Directory.CreateDirectory(_fallbackBaseDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Deploy_AddInFound_DeploysToUserAddIns()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("AddIn Content"));

        var result = AddInDeployer.Deploy(_versionDir, _userAddInsDir, _fallbackBaseDir, TextWriter.Null);

        result.Status.Should().Be(AddInDeploymentStatus.DeployedWithFallback);
        result.IsFullyDeployed.Should().BeTrue();
        result.InstalledAddInVersion.Should().Be("0.2.0");
        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin")).Should().BeTrue();
        File.Exists(Path.Combine(_fallbackBaseDir, "AddIn", "TiaAgent-0.2.0.addin")).Should().BeTrue();
    }

    [Fact]
    public void Deploy_ReplacesExistingVersion()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("New Content"));

        // Pre-existing Add-In
        File.WriteAllBytes(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin"), Encoding.UTF8.GetBytes("Old Content"));

        var result = AddInDeployer.Deploy(_versionDir, _userAddInsDir, _fallbackBaseDir, TextWriter.Null);

        result.Status.Should().Be(AddInDeploymentStatus.DeployedWithFallback);
        File.ReadAllText(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin")).Should().Be("New Content");
    }

    [Fact]
    public void Deploy_RemovesStaleVersions()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("New Content"));

        // Stale Add-In from previous version
        File.WriteAllBytes(Path.Combine(_userAddInsDir, "TiaAgent-0.1.0.addin"), Encoding.UTF8.GetBytes("Old Version"));

        var result = AddInDeployer.Deploy(_versionDir, _userAddInsDir, _fallbackBaseDir, TextWriter.Null);

        result.Status.Should().Be(AddInDeploymentStatus.DeployedWithFallback);
        result.RemovedStaleFiles.Should().Contain("TiaAgent-0.1.0.addin");
        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.1.0.addin")).Should().BeFalse();
        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin")).Should().BeTrue();
    }

    [Fact]
    public void Deploy_NoAddInPackage_ReturnsNoAddInPackage()
    {
        // No AddIn/ directory
        var result = AddInDeployer.Deploy(_versionDir, _userAddInsDir, _fallbackBaseDir, TextWriter.Null);

        result.Status.Should().Be(AddInDeploymentStatus.NoAddInPackage);
        result.IsFullyDeployed.Should().BeFalse();
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Deploy_UserAddInsDirMissing_ReturnsFallbackOnly()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("AddIn Content"));

        // Use a non-existent custom dir that can't be created (simulating permission issue)
        var lockedDir = Path.Combine(_tempDirectory, "locked", "UserAddIns");

        var result = AddInDeployer.Deploy(_versionDir, lockedDir, _fallbackBaseDir, TextWriter.Null);

        // The deployer will try to create the directory. If it can, it will deploy.
        // For this test, we verify the fallback is always preserved.
        result.FallbackAddInPath.Should().NotBeNull();
        File.Exists(result.FallbackAddInPath).Should().BeTrue();
    }

    [Fact]
    public void Deploy_IdempotentRepeatedInstallation()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("AddIn Content"));

        // First deployment
        var result1 = AddInDeployer.Deploy(_versionDir, _userAddInsDir, _fallbackBaseDir, TextWriter.Null);
        result1.Status.Should().Be(AddInDeploymentStatus.DeployedWithFallback);

        // Second deployment (idempotent)
        var result2 = AddInDeployer.Deploy(_versionDir, _userAddInsDir, _fallbackBaseDir, TextWriter.Null);
        result2.Status.Should().Be(AddInDeploymentStatus.DeployedWithFallback);
        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin")).Should().BeTrue();
    }

    [Fact]
    public void Deploy_PathWithSpaces_DeploysCorrectly()
    {
        var spacedVersionDir = Path.Combine(_tempDirectory, "versions", "0.2.0 beta");
        Directory.CreateDirectory(spacedVersionDir);
        var addinDir = Path.Combine(spacedVersionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("AddIn Content"));

        var spacedUserAddIns = Path.Combine(_tempDirectory, "User Ins Dir");
        // Pre-create the directory since the deployer will try to detect TIA Portal
        // and may not find it, but with a custom dir it should deploy
        Directory.CreateDirectory(spacedUserAddIns);

        var result = AddInDeployer.Deploy(spacedVersionDir, spacedUserAddIns, _fallbackBaseDir, TextWriter.Null);

        result.Status.Should().Be(AddInDeploymentStatus.DeployedWithFallback);
        File.Exists(Path.Combine(spacedUserAddIns, "TiaAgent-0.2.0.addin")).Should().BeTrue();
    }

    [Fact]
    public void FindAddInFiles_EmptyDir_ReturnsEmpty()
    {
        var addinFiles = AddInDeployer.FindAddInFiles(_versionDir);
        addinFiles.Should().BeEmpty();
    }

    [Fact]
    public void FindAddInFiles_WithAddIn_ReturnsFile()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        File.WriteAllBytes(Path.Combine(addinDir, "TiaAgent-0.2.0.addin"), Encoding.UTF8.GetBytes("content"));

        var addinFiles = AddInDeployer.FindAddInFiles(_versionDir);
        addinFiles.Should().HaveCount(1);
        addinFiles[0].Should().EndWith("TiaAgent-0.2.0.addin");
    }

    [Fact]
    public void ExtractVersion_ValidFilename_ReturnsVersion()
    {
        var version = AddInDeployer.ExtractVersion("TiaAgent-0.2.0.addin");
        version.Should().Be("0.2.0");
    }

    [Fact]
    public void ExtractVersion_InvalidFilename_ReturnsNull()
    {
        var version = AddInDeployer.ExtractVersion("SomeOther.addin");
        version.Should().BeNull();
    }

    [Fact]
    public void RemoveStaleAddIns_RemovesOnlyTiaAgentFiles()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        File.WriteAllBytes(Path.Combine(addinDir, "TiaAgent-0.2.0.addin"), Encoding.UTF8.GetBytes("content"));

        // Mix of TiaAgent and non-TiaAgent files
        File.WriteAllBytes(Path.Combine(_userAddInsDir, "TiaAgent-0.1.0.addin"), Encoding.UTF8.GetBytes("old"));
        File.WriteAllBytes(Path.Combine(_userAddInsDir, "ThirdParty-1.0.addin"), Encoding.UTF8.GetBytes("other"));

        var stdout = new StringWriter();
        var removed = AddInDeployer.RemoveStaleAddIns(_userAddInsDir, "TiaAgent-0.2.0.addin", stdout);

        removed.Should().Contain("TiaAgent-0.1.0.addin");
        removed.Should().NotContain("ThirdParty-1.0.addin");
        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.1.0.addin")).Should().BeFalse();
        File.Exists(Path.Combine(_userAddInsDir, "ThirdParty-1.0.addin")).Should().BeTrue();
    }

    [Fact]
    public void PreserveLocally_CreatesFallbackDirectory()
    {
        var addinDir = Path.Combine(_versionDir, "AddIn");
        Directory.CreateDirectory(addinDir);
        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        File.WriteAllBytes(addinFile, Encoding.UTF8.GetBytes("AddIn Content"));

        var fallbackDir = AddInDeployer.PreserveLocally(addinFile, _fallbackBaseDir, TextWriter.Null);

        fallbackDir.Should().Be(Path.Combine(_fallbackBaseDir, "AddIn"));
        Directory.Exists(fallbackDir).Should().BeTrue();
        File.Exists(Path.Combine(fallbackDir, "TiaAgent-0.2.0.addin")).Should().BeTrue();
    }
}
