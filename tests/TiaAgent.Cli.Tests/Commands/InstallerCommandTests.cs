using System;
using System.IO;
using System.Text;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Payload;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class InstallerCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;
    private readonly string _userAddInsDir;
    private readonly string _payloadDir;

    public InstallerCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "InstallerCommandTests_" + Guid.NewGuid().ToString("N"));
        _customRoot = Path.Combine(_tempDirectory, "TiaAgentRoot");
        _userAddInsDir = Path.Combine(_tempDirectory, "UserAddIns");
        _payloadDir = Path.Combine(_tempDirectory, "payload");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_customRoot);
        Directory.CreateDirectory(_userAddInsDir);
        Directory.CreateDirectory(_payloadDir);

        CreateDummyPayload(_payloadDir, "0.2.0-beta.1");
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
    public void InstallCommand_WithValidPayload_InstallsSuccessfully()
    {
        var options = new InstallOptions
        {
            Version = "0.2.0-beta.1",
            PayloadDir = _payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = InstallCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        stderr.ToString().Should().BeEmpty();
        stdout.ToString().Should().Contain("Successfully installed TIA Agent v0.2.0-beta.1");

        var layout = new TiaAgentLayout(_customRoot);
        File.Exists(layout.CurrentManifestPath).Should().BeTrue();
        File.Exists(layout.InstallationsManifestPath).Should().BeTrue();
        File.Exists(layout.ConfigPath).Should().BeTrue();

        var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        current.ActiveVersion.Should().Be("0.2.0-beta.1");

        var installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        installations.Versions.Should().ContainKey("0.2.0-beta.1");

        var versionDir = layout.GetVersionPath("0.2.0-beta.1");
        Directory.Exists(versionDir).Should().BeTrue();
        File.Exists(Path.Combine(versionDir, "Bridge", "TiaAgent.Bridge.dll")).Should().BeTrue();

        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin")).Should().BeTrue();
    }

    [Fact]
    public void InstallCommand_AlreadyInstalled_UpdatesActiveAndReturnsZero()
    {
        var options = new InstallOptions
        {
            Version = "0.2.0-beta.1",
            PayloadDir = _payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        InstallCommand.Execute(options, TextWriter.Null, TextWriter.Null);

        using var stdout = new StringWriter();
        var exitCode = InstallCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("is already installed");
    }

    [Fact]
    public void InstallCommand_WithForce_ReinstallsSuccessfully()
    {
        var options = new InstallOptions
        {
            Version = "0.2.0-beta.1",
            PayloadDir = _payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        InstallCommand.Execute(options, TextWriter.Null, TextWriter.Null);

        options.Force = true;
        using var stdout = new StringWriter();
        var exitCode = InstallCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully installed TIA Agent v0.2.0-beta.1");
    }

    [Fact]
    public void InstallCommand_WithInvalidPayload_ReturnsError()
    {
        var emptyPayloadDir = Path.Combine(_tempDirectory, "empty_payload");
        Directory.CreateDirectory(emptyPayloadDir);

        var options = new InstallOptions
        {
            PayloadDir = emptyPayloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stderr = new StringWriter();
        var exitCode = InstallCommand.Execute(options, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Payload validation failed");
    }

    [Fact]
    public void UninstallCommand_WithSpecificVersion_UninstallsVersion()
    {
        var installOptions = new InstallOptions
        {
            Version = "0.2.0-beta.1",
            PayloadDir = _payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };
        InstallCommand.Execute(installOptions, TextWriter.Null, TextWriter.Null);

        var uninstallOptions = new UninstallOptions
        {
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        var exitCode = UninstallCommand.Execute(uninstallOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully uninstalled TIA Agent version(s): 0.2.0-beta.1");

        var layout = new TiaAgentLayout(_customRoot);
        Directory.Exists(layout.GetVersionPath("0.2.0-beta.1")).Should().BeFalse();
        File.Exists(Path.Combine(_userAddInsDir, "TiaAgent-0.2.0.addin")).Should().BeFalse();
    }

    [Fact]
    public void UninstallCommand_WithAllFlag_UninstallsAllVersions()
    {
        var installOptions = new InstallOptions
        {
            Version = "0.2.0-beta.1",
            PayloadDir = _payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };
        InstallCommand.Execute(installOptions, TextWriter.Null, TextWriter.Null);

        var uninstallOptions = new UninstallOptions
        {
            All = true,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        var exitCode = UninstallCommand.Execute(uninstallOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully uninstalled TIA Agent version(s)");

        var layout = new TiaAgentLayout(_customRoot);
        File.Exists(layout.CurrentManifestPath).Should().BeFalse();

        var installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        installations.Versions.Should().BeEmpty();
    }

    [Fact]
    public void UninstallCommand_NonExistentVersion_ReturnsError()
    {
        var uninstallOptions = new UninstallOptions
        {
            Version = "9.9.9",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stderr = new StringWriter();
        var exitCode = UninstallCommand.Execute(uninstallOptions, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Version '9.9.9' is not installed");
    }

    private static void CreateDummyPayload(string payloadDir, string version)
    {
        var bridgeDir = Path.Combine(payloadDir, "Bridge");
        var addinDir = Path.Combine(payloadDir, "AddIn");
        Directory.CreateDirectory(bridgeDir);
        Directory.CreateDirectory(addinDir);

        var bridgeDll = Path.Combine(bridgeDir, "TiaAgent.Bridge.dll");
        var bridgeContent = Encoding.UTF8.GetBytes("Bridge DLL Content");
        File.WriteAllBytes(bridgeDll, bridgeContent);

        var addinFile = Path.Combine(addinDir, "TiaAgent-0.2.0.addin");
        var addinContent = Encoding.UTF8.GetBytes("AddIn Content");
        File.WriteAllBytes(addinFile, addinContent);

        var bridgeHash = PayloadStore.ComputeSha256(bridgeDll);
        var addinHash = PayloadStore.ComputeSha256(addinFile);

        var manifest = new PayloadManifest
        {
            ProductVersion = version,
            CommitSha = "testsha",
            Components =
            {
                ["bridge"] = new PayloadComponentMetadata
                {
                    RelativePath = "Bridge/TiaAgent.Bridge.dll",
                    Version = version,
                    Sha256Hash = bridgeHash,
                    SizeBytes = bridgeContent.Length
                },
                ["addin"] = new PayloadComponentMetadata
                {
                    RelativePath = "AddIn/TiaAgent-0.2.0.addin",
                    Version = version,
                    Sha256Hash = addinHash,
                    SizeBytes = addinContent.Length
                }
            },
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "Bridge/TiaAgent.Bridge.dll",
                    Sha256Hash = bridgeHash,
                    SizeBytes = bridgeContent.Length
                },
                new PayloadFileEntry
                {
                    RelativePath = "AddIn/TiaAgent-0.2.0.addin",
                    Sha256Hash = addinHash,
                    SizeBytes = addinContent.Length
                }
            }
        };

        PayloadStore.WriteManifest(payloadDir, manifest);
    }
}
