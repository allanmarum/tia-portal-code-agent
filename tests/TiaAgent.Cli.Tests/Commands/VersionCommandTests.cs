using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class VersionCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;

    public VersionCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "VersionCommandTests_" + Guid.NewGuid().ToString("N"));
        _customRoot = Path.Combine(_tempDirectory, "TiaAgentRoot");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_customRoot);
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
    public void VersionCommand_Default_OutputsVersionString()
    {
        var options = new VersionOptions
        {
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();

        var exitCode = VersionCommand.Execute(options, stdout);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("tia-agent version");
    }

    [Fact]
    public void VersionCommand_Verbose_OutputsDetailedDiagnostics()
    {
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();

        var current = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = "0.2.0-test",
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "test"
        };
        ManifestStore.WriteAtomic(layout.CurrentManifestPath, current);

        var options = new VersionOptions
        {
            CustomRoot = _customRoot,
            Verbose = true
        };

        using var stdout = new StringWriter();

        var exitCode = VersionCommand.Execute(options, stdout);

        exitCode.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("TIA Agent CLI");
        output.Should().Contain("Product Version:");
        output.Should().Contain("Active Version:      0.2.0-test");
        output.Should().Contain("OS Environment:");
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void VersionCommand_Json_OutputsJsonReport()
    {
        var options = new VersionOptions
        {
            CustomRoot = _customRoot,
            Json = true
        };

        using var stdout = new StringWriter();

        var exitCode = VersionCommand.Execute(options, stdout);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        var report = JsonSerializer.Deserialize<VersionReport>(json, s_jsonOptions);

        report.Should().NotBeNull();
        report!.ProductVersion.Should().NotBeNullOrWhiteSpace();
        report.OsEnvironment.Should().NotBeNullOrWhiteSpace();
    }
}
