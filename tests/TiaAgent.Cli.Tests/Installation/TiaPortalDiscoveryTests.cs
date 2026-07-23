using System;
using System.IO;
using FluentAssertions;
using TiaAgent.Cli.Installation;
using Xunit;

namespace TiaAgent.Cli.Tests.Installation;

public sealed class TiaPortalDiscoveryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _originalTiaPublicApiDir;

    public TiaPortalDiscoveryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiaPortalDiscoveryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _originalTiaPublicApiDir = Environment.GetEnvironmentVariable("TiaPublicApiDir") ?? string.Empty;
    }

    public void Dispose()
    {
        // Restore original env var
        if (string.IsNullOrEmpty(_originalTiaPublicApiDir))
        {
            Environment.SetEnvironmentVariable("TiaPublicApiDir", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("TiaPublicApiDir", _originalTiaPublicApiDir);
        }

        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Discover_WithCustomDir_ReturnsCustomDir()
    {
        var customDir = Path.Combine(_tempDirectory, "CustomAddIns");
        Directory.CreateDirectory(customDir);

        var result = TiaPortalDiscovery.Discover(customDir);

        result.UserAddInsDirectory.Should().Be(customDir);
        result.UserAddInsDirectoryExists.Should().BeTrue();
        result.DetectionSource.Should().Be("cli-override");
    }

    [Fact]
    public void Discover_WithCustomDir_NotExists_ReturnsOverrideButNotExists()
    {
        var customDir = Path.Combine(_tempDirectory, "NonExistent");

        var result = TiaPortalDiscovery.Discover(customDir);

        result.UserAddInsDirectory.Should().Be(customDir);
        result.UserAddInsDirectoryExists.Should().BeFalse();
        result.DetectionSource.Should().Be("cli-override");
        result.TiaPortalDetected.Should().BeTrue();
    }

    [Fact]
    public void Discover_WithEnvVar_DetectsTiaPortal()
    {
        // Create a fake TIA Portal API directory structure
        var fakeApiDir = Path.Combine(_tempDirectory, "Portal V21", "PublicAPI", "V21", "net48");
        Directory.CreateDirectory(fakeApiDir);
        File.WriteAllText(Path.Combine(fakeApiDir, "Siemens.Engineering.Base.dll"), "fake");

        Environment.SetEnvironmentVariable("TiaPublicApiDir", fakeApiDir);

        var result = TiaPortalDiscovery.Discover();

        result.TiaPortalDetected.Should().BeTrue();
        result.DetectionSource.Should().Be("env-var");
        result.UserAddInsDirectory.Should().Contain("UserAddIns");
        // UserAddIns may or may not exist depending on whether TIA Portal is installed on the machine
        result.UserAddInsDirectory.Should().Be(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Siemens", "Automation", "Portal V21", "UserAddIns"));
    }

    [Fact]
    public void Discover_WithPathWithSpaces_HandlesCorrectly()
    {
        var customDir = Path.Combine(_tempDirectory, "Path With Spaces", "UserAddIns");
        Directory.CreateDirectory(customDir);

        var result = TiaPortalDiscovery.Discover(customDir);

        result.UserAddInsDirectory.Should().Be(customDir);
        result.UserAddInsDirectoryExists.Should().BeTrue();
    }

    [Fact]
    public void DeriveTiaRootFromApiDir_ReturnsCorrectRoot()
    {
        var apiDir = Path.Combine(_tempDirectory, "Portal V21", "PublicAPI", "V21", "net48");
        Directory.CreateDirectory(apiDir);

        var root = TiaPortalDiscovery.DeriveTiaRootFromApiDir(apiDir);

        root.Should().Be(Path.Combine(_tempDirectory, "Portal V21"));
    }

    [Fact]
    public void Discover_NullCustomDir_ReturnsConsistentResult()
    {
        // With no env var override, should return a consistent result
        // (may detect TIA Portal if installed on the machine)
        Environment.SetEnvironmentVariable("TiaPublicApiDir", null);

        var result = TiaPortalDiscovery.Discover(null);

        // Should always have a UserAddInsDirectory path
        result.UserAddInsDirectory.Should().NotBeNullOrWhiteSpace();
        result.UserAddInsDirectory.Should().Contain("UserAddIns");
    }
}
