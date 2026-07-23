using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class DoctorCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;
    private readonly string _userAddInsDir;

    public DoctorCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DoctorCommandTests_" + Guid.NewGuid().ToString("N"));
        _customRoot = Path.Combine(_tempDirectory, "TiaAgentRoot");
        _userAddInsDir = Path.Combine(_tempDirectory, "UserAddIns");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_customRoot);
        Directory.CreateDirectory(_userAddInsDir);
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
    public void DoctorCommand_WithEmptyRoot_ReturnsZeroWithWarnings()
    {
        var options = new DoctorOptions
        {
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DoctorCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("TIA Agent Doctor Diagnostics");
        stdout.ToString().Should().Contain("Diagnostic Summary");
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void DoctorCommand_WithJsonOption_ReturnsValidJsonReport()
    {
        var options = new DoctorOptions
        {
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir,
            Json = true
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DoctorCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);

        var output = stdout.ToString();
        var report = JsonSerializer.Deserialize<DoctorReport>(output, s_jsonOptions);

        report.Should().NotBeNull();
        report!.Checks.Should().NotBeEmpty();
        report.OverallStatus.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DoctorCommand_WithCorruptedConfig_ReturnsErrorExitCode()
    {
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        File.WriteAllText(layout.ConfigPath, "{ malformed json ");

        var options = new DoctorOptions
        {
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DoctorCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stdout.ToString().Should().Contain("[FAIL]");
        stdout.ToString().Should().Contain("Malformed config.json");
    }

    [Fact]
    public void DoctorCommand_WithMissingActiveVersionFolder_ReturnsErrorExitCode()
    {
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();

        var current = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = "0.9.9-ghost",
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "test"
        };
        ManifestStore.WriteAtomic(layout.CurrentManifestPath, current);

        var options = new DoctorOptions
        {
            CustomRoot = _customRoot,
            UserAddInsDir = _userAddInsDir
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DoctorCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stdout.ToString().Should().Contain("[FAIL]");
        stdout.ToString().Should().Contain("folder");
    }
}
