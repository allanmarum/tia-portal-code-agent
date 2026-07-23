using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Supervisor;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

[Collection("ConsoleTests")]
public sealed class SupervisorCommandTests : IDisposable
{
    private static readonly string[] s_startHelpArgs = ["start", "--help"];
    private static readonly string[] s_stopHelpArgs = ["stop", "--help"];
    private static readonly string[] s_statusHelpArgs = ["status", "--help"];

    private readonly string _tempDirectory;
    private readonly string _customRoot;

    public SupervisorCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SupervisorCommandTests_" + Guid.NewGuid().ToString("N"));
        _customRoot = Path.Combine(_tempDirectory, "TiaAgentRoot");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_customRoot);

        var configDir = Path.Combine(_customRoot, "config");
        Directory.CreateDirectory(configDir);
        var settingsJson = """
        {
          "preferredPorts": {
            "bridge": 43190,
            "opencode": 43191
          },
          "portRange": {
            "start": 43190,
            "end": 43199
          }
        }
        """;
        File.WriteAllText(Path.Combine(configDir, "settings.json"), settingsJson);
    }

    public void Dispose()
    {
        var stopOptions = new StopOptions { CustomRoot = _customRoot, Force = true };
        using var sw = new StringWriter();
        StopCommand.Execute(stopOptions, sw, sw);

        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void StatusCommand_NoManifest_OutputsNotRunningStatus()
    {
        var options = new StatusOptions { CustomRoot = _customRoot };
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = StatusCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("TIA Agent Runtime");
        output.Should().Contain("Status     : unknown");
        output.Should().Contain("Supervisor : Not running");
    }

    [Fact]
    public void StatusCommand_Json_OutputsJsonStatusResult()
    {
        var options = new StatusOptions { CustomRoot = _customRoot, Json = true };
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = StatusCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        json.Should().Contain("\"status\": \"unknown\"");
        json.Should().Contain("\"supervisor\"");
        json.Should().Contain("\"bridge\"");
        json.Should().Contain("\"opencode\"");
    }

    [Fact]
    public void StopCommand_NoManifest_ReturnsSuccess()
    {
        var options = new StopOptions { CustomRoot = _customRoot };
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = StopCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Nothing to stop");
    }

    [Fact]
    public void StartCommand_NoMonitorMode_InitializesRuntimeManifest()
    {
        var options = new StartOptions
        {
            CustomRoot = _customRoot,
            NoMonitor = true
        };
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = StartCommand.Execute(options, stdout, stderr);

        var manifestPath = Path.Combine(_customRoot, "runtime", "runtime.json");
        File.Exists(manifestPath).Should().BeTrue();

        var json = File.ReadAllText(manifestPath);
        json.Should().Contain("instanceId");
    }

    [Fact]
    public void PortAllocator_ReturnsAvailablePortInRange()
    {
        int port = PortAllocator.GetAvailablePort(43119);
        port.Should().BeGreaterThan(0);
        port.Should().BeInRange(43100, 43200);
    }

    [Fact]
    public void Program_StartHelp_OutputsUsage()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var currentOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            var exitCode = Program.Main(s_startHelpArgs);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Usage: tia-agent start");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Fact]
    public void Program_StopHelp_OutputsUsage()
    {
        using var stdout = new StringWriter();
        var currentOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            var exitCode = Program.Main(s_stopHelpArgs);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Usage: tia-agent stop");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Fact]
    public void Program_StatusHelp_OutputsUsage()
    {
        using var stdout = new StringWriter();
        var currentOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            var exitCode = Program.Main(s_statusHelpArgs);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Usage: tia-agent status");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }
}
