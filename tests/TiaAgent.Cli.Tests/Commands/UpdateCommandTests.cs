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

public sealed class UpdateCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;
    private readonly string _userAddInsDir;
    private readonly string _payloadDirV1;
    private readonly string _payloadDirV2;

    public UpdateCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UpdateCommandTests_" + Guid.NewGuid().ToString("N"));
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
    public void UpdateCommand_WithTargetInstalledVersion_UpdatesActiveVersionSuccessfully()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        InstallVersion("0.2.0-rc.1", _payloadDirV2);
        ActivateVersion("0.2.0-beta.1");

        var updateOptions = new UpdateOptions
        {
            Version = "0.2.0-rc.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = UpdateCommand.Execute(updateOptions, stdout, stderr);

        exitCode.Should().Be(0);
        stderr.ToString().Should().BeEmpty();
        stdout.ToString().Should().Contain("Successfully updated TIA Agent to version '0.2.0-rc.1'");

        var layout = new TiaAgentLayout(_customRoot);
        var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        current.ActiveVersion.Should().Be("0.2.0-rc.1");
        current.PreviousVersion.Should().Be("0.2.0-beta.1");
        current.ActivatedBy.Should().Be("tia-agent update");
    }

    [Fact]
    public void UpdateCommand_WithPayloadDirectory_InstallsAndUpdatesToPayloadVersion()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var updateOptions = new UpdateOptions
        {
            PayloadDir = _payloadDirV2,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = UpdateCommand.Execute(updateOptions, stdout, stderr);

        exitCode.Should().Be(0);

        var layout = new TiaAgentLayout(_customRoot);
        var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        current.ActiveVersion.Should().Be("0.2.0-rc.1");
        current.PreviousVersion.Should().Be("0.2.0-beta.1");
    }

    [Fact]
    public void UpdateCommand_AlreadyActiveVersionWithoutForce_ReportsAlreadyActive()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var updateOptions = new UpdateOptions
        {
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        var exitCode = UpdateCommand.Execute(updateOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("already active");
    }

    [Fact]
    public void UpdateCommand_AlreadyActiveVersionWithForce_ReactivatesVersion()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var updateOptions = new UpdateOptions
        {
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir,
            Force = true
        };

        using var stdout = new StringWriter();
        var exitCode = UpdateCommand.Execute(updateOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully updated TIA Agent to version '0.2.0-beta.1'");
    }

    [Fact]
    public void UpdateCommand_UninstalledVersionNoPayload_ReturnsError()
    {
        var updateOptions = new UpdateOptions
        {
            Version = "9.9.9",
            CustomRoot = _customRoot
        };

        using var stderr = new StringWriter();
        var exitCode = UpdateCommand.Execute(updateOptions, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Version '9.9.9' is not installed and no valid payload was found");
    }

    [Fact]
    public void UpdateCommand_JsonOutput_ReturnsStructuredReport()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        InstallVersion("0.2.0-rc.1", _payloadDirV2);
        ActivateVersion("0.2.0-beta.1");

        var updateOptions = new UpdateOptions
        {
            Version = "0.2.0-rc.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir,
            Json = true
        };

        using var stdout = new StringWriter();
        var exitCode = UpdateCommand.Execute(updateOptions, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("activeVersion").GetString().Should().Be("0.2.0-rc.1");
        doc.RootElement.GetProperty("previousVersion").GetString().Should().Be("0.2.0-beta.1");
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

    private void ActivateVersion(string version)
    {
        var activateOptions = new ActivateOptions
        {
            Version = version,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };
        ActivateCommand.Execute(activateOptions, TextWriter.Null, TextWriter.Null);
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
