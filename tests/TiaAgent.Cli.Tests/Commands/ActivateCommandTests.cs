using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Payload;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class ActivateCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;
    private readonly string _userAddInsDir;
    private readonly string _payloadDirV1;
    private readonly string _payloadDirV2;

    public ActivateCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ActivateCommandTests_" + Guid.NewGuid().ToString("N"));
        _customRoot = Path.Combine(_tempDirectory, "TiaAgentRoot");
        _userAddInsDir = Path.Combine(_tempDirectory, "UserAddIns");
        _payloadDirV1 = Path.Combine(_tempDirectory, "payload_v1");
        _payloadDirV2 = Path.Combine(_tempDirectory, "payload_v2");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_customRoot);
        Directory.CreateDirectory(_userAddInsDir);
        Directory.CreateDirectory(_payloadDirV1);
        Directory.CreateDirectory(_payloadDirV2);

        CreateDummyPayload(_payloadDirV1, "0.2.0-beta.1");
        CreateDummyPayload(_payloadDirV2, "0.2.0-rc.1");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Failed to remove test directory '{_tempDirectory}': {ex}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Failed to remove test directory '{_tempDirectory}': {ex}");
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ActivateCommand_WithValidInstalledVersion_ActivatesSuccessfully()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var activateOptions = new ActivateOptions
        {
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ActivateCommand.Execute(activateOptions, stdout, stderr);

        exitCode.Should().Be(0);
        stderr.ToString().Should().BeEmpty();
        stdout.ToString().Should().Contain("Successfully activated TIA Agent version '0.2.0-beta.1'");

        var layout = new TiaAgentLayout(_customRoot);
        File.Exists(layout.CurrentManifestPath).Should().BeTrue();

        var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        current.ActiveVersion.Should().Be("0.2.0-beta.1");
        current.ActivatedBy.Should().Be("tia-agent activate");
    }

    [Fact]
    public void ActivateCommand_SwitchingVersions_UpdatesCurrentManifestAndDeploysAddIn()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        InstallVersion("0.2.0-rc.1", _payloadDirV2);

        var layout = new TiaAgentLayout(_customRoot);
        var initialCurrent = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        initialCurrent.ActiveVersion.Should().Be("0.2.0-rc.1");

        var activateOptions = new ActivateOptions
        {
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        var exitCode = ActivateCommand.Execute(activateOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully activated TIA Agent version '0.2.0-beta.1'");

        var updatedCurrent = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        updatedCurrent.ActiveVersion.Should().Be("0.2.0-beta.1");

        var installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        installations.Versions.Should().ContainKey("0.2.0-beta.1");
        installations.Versions.Should().ContainKey("0.2.0-rc.1");
    }

    [Fact]
    public void ActivateCommand_UninstalledVersion_ReturnsError()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var activateOptions = new ActivateOptions
        {
            Version = "9.9.9",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stderr = new StringWriter();
        var exitCode = ActivateCommand.Execute(activateOptions, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Version '9.9.9' is not installed");
        stderr.ToString().Should().Contain("Installed versions: 0.2.0-beta.1");
    }

    [Fact]
    public void ActivateCommand_NoVersionSpecified_ReturnsError()
    {
        var activateOptions = new ActivateOptions
        {
            Version = null,
            CustomRoot = _customRoot
        };

        using var stderr = new StringWriter();
        var exitCode = ActivateCommand.Execute(activateOptions, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Version to activate must be specified");
    }

    [Fact]
    public void ActivateCommand_JsonOutput_ReturnsStructuredReport()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var activateOptions = new ActivateOptions
        {
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir,
            Json = true
        };

        using var stdout = new StringWriter();
        var exitCode = ActivateCommand.Execute(activateOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("activeVersion").GetString().Should().Be("0.2.0-beta.1");
    }

    [Fact]
    public void ActivateCommand_WithForce_ActivatesUninstalledVersion()
    {
        var activateOptions = new ActivateOptions
        {
            Version = "0.2.0-forced",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir,
            Force = true
        };

        using var stdout = new StringWriter();
        var exitCode = ActivateCommand.Execute(activateOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);

        var layout = new TiaAgentLayout(_customRoot);
        var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        current.ActiveVersion.Should().Be("0.2.0-forced");
    }

    private void InstallVersion(string version, string payloadDir)
    {
        var installOptions = new InstallOptions
        {
            Version = version,
            PayloadDir = payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };
        InstallCommand.Execute(installOptions, TextWriter.Null, TextWriter.Null);
    }

    private static void CreateDummyPayload(string payloadDir, string version)
    {
        var bridgeDir = Path.Combine(payloadDir, "Bridge");
        var addinDir = Path.Combine(payloadDir, "AddIn");
        Directory.CreateDirectory(bridgeDir);
        Directory.CreateDirectory(addinDir);

        var bridgeDll = Path.Combine(bridgeDir, "TiaAgent.Bridge.dll");
        var bridgeContent = Encoding.UTF8.GetBytes("Bridge Content " + version);
        File.WriteAllBytes(bridgeDll, bridgeContent);

        var addinFile = Path.Combine(addinDir, $"TiaAgent-{version}.addin");
        var addinContent = Encoding.UTF8.GetBytes("AddIn Content " + version);
        File.WriteAllBytes(addinFile, addinContent);

        var bridgeHash = PayloadStore.ComputeSha256(bridgeDll);
        var addinHash = PayloadStore.ComputeSha256(addinFile);

        var manifest = new PayloadManifest
        {
            ProductVersion = version,
            CommitSha = "sha-" + version,
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
                    RelativePath = $"AddIn/TiaAgent-{version}.addin",
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
                    RelativePath = $"AddIn/TiaAgent-{version}.addin",
                    Sha256Hash = addinHash,
                    SizeBytes = addinContent.Length
                }
            }
        };

        PayloadStore.WriteManifest(payloadDir, manifest);
    }
}
