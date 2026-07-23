using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Payload;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class VersionsCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;
    private readonly string _userAddInsDir;
    private readonly string _payloadDirV1;
    private readonly string _payloadDirV2;

    public VersionsCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "VersionsCommandTests_" + Guid.NewGuid().ToString("N"));
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

        // Set channel to "beta" so prerelease versions (beta, rc) are compatible
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "beta" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);
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
    public void VersionsCommand_List_OutputsInstalledVersionsAndActiveMarker()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        UpdateVersion("0.2.0-rc.1", _payloadDirV2);

        var options = new VersionsOptions
        {
            Subcommand = "list",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        var exitCode = VersionsCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("0.2.0-rc.1");
        output.Should().Contain("[active]");
        output.Should().Contain("0.2.0-beta.1");
        output.Should().Contain("[rollback candidate]");
    }

    [Fact]
    public void VersionsCommand_RemoveActiveVersionWithoutForce_Fails()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var options = new VersionsOptions
        {
            Subcommand = "remove",
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = VersionsCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Cannot remove version '0.2.0-beta.1' because it is currently active.");
    }

    [Fact]
    public void VersionsCommand_RemoveRollbackCandidateWithoutForce_Fails()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        UpdateVersion("0.2.0-rc.1", _payloadDirV2);

        var options = new VersionsOptions
        {
            Subcommand = "remove",
            Version = "0.2.0-beta.1",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = VersionsCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Cannot remove version '0.2.0-beta.1' because it is the only known-good rollback version.");
    }

    [Fact]
    public void VersionsCommand_RemoveRollbackCandidateWithForce_Succeeds()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        UpdateVersion("0.2.0-rc.1", _payloadDirV2);

        var options = new VersionsOptions
        {
            Subcommand = "remove",
            Version = "0.2.0-beta.1",
            Force = true,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = VersionsCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully removed TIA Agent version '0.2.0-beta.1'");

        var layout = new TiaAgentLayout(_customRoot);
        var installations = ManifestStore.Read<InstallationsManifest>(layout.InstallationsManifestPath);
        installations.Versions.Should().NotContainKey("0.2.0-beta.1");
    }

    [Fact]
    public void VersionsCommand_ListJsonOutput_ReturnsStructuredReport()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);
        UpdateVersion("0.2.0-rc.1", _payloadDirV2);

        var options = new VersionsOptions
        {
            Subcommand = "list",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir,
            Json = true
        };

        using var stdout = new StringWriter();
        var exitCode = VersionsCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("activeVersion").GetString().Should().Be("0.2.0-rc.1");
        doc.RootElement.GetProperty("rollbackVersion").GetString().Should().Be("0.2.0-beta.1");
        var installed = doc.RootElement.GetProperty("installedVersions");
        installed.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void VersionsCommand_RemoveNonExistentVersion_Fails()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var options = new VersionsOptions
        {
            Subcommand = "remove",
            Version = "9.9.9",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = VersionsCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Version '9.9.9' is not installed.");
    }

    [Fact]
    public void VersionsCommand_RemoveWithoutVersion_Fails()
    {
        InstallVersion("0.2.0-beta.1", _payloadDirV1);

        var options = new VersionsOptions
        {
            Subcommand = "remove",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = VersionsCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Version to remove must be specified.");
    }

    [Fact]
    public void VersionsCommand_ListEmpty_ShowsNoVersionsMessage()
    {
        var options = new VersionsOptions
        {
            Subcommand = "list",
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        var exitCode = VersionsCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("No TIA Agent versions are currently installed.");
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

    private void UpdateVersion(string version, string payloadDir)
    {
        var updateOptions = new UpdateOptions
        {
            Version = version,
            PayloadDir = payloadDir,
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };
        UpdateCommand.Execute(updateOptions, TextWriter.Null, TextWriter.Null);
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
