using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

[Collection("ConsoleTests")]
public sealed class RuntimeCommandTests : IDisposable
{
    private static readonly string[] s_runtimeHelpArgs = ["runtime", "--help"];

    private readonly string _tempDirectory;
    private readonly string _customRoot;

    public RuntimeCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "RuntimeCommandTests_" + Guid.NewGuid().ToString("N"));
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
    public void RuntimeCommand_List_DisplaysAllRegisteredRuntimes()
    {
        var options = new RuntimeOptions
        {
            Subcommand = "list",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("Default Runtime:");
        output.Should().Contain("opencode");
        output.Should().Contain("mimo");
        output.Should().Contain("claude");
    }

    [Fact]
    public void RuntimeCommand_List_WithJsonOption_ReturnsValidJson()
    {
        var options = new RuntimeOptions
        {
            Subcommand = "list",
            CustomRoot = _customRoot,
            Json = true
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        json.Should().Contain("\"defaultRuntime\":");
        json.Should().Contain("\"runtimes\":");
        json.Should().Contain("\"opencode\"");
        json.Should().Contain("\"mimo\"");
        json.Should().Contain("\"claude\"");
    }

    [Fact]
    public void RuntimeCommand_Use_ValidRuntime_UpdatesConfig()
    {
        var useOptions = new RuntimeOptions
        {
            Subcommand = "use",
            RuntimeId = "mimo",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = RuntimeCommand.Execute(useOptions, stdout, stderr);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Set default runtime to 'mimo'");
        }

        var config = ConfigCommand.LoadConfig(new TiaAgentLayout(_customRoot).ConfigPath);
        config.DefaultRuntime.Should().Be("mimo");
    }

    [Fact]
    public void RuntimeCommand_Use_WithMode_UpdatesConfigMode()
    {
        var useOptions = new RuntimeOptions
        {
            Subcommand = "use",
            RuntimeId = "opencode",
            Mode = "cli",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = RuntimeCommand.Execute(useOptions, stdout, stderr);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Set mode for 'opencode' to 'cli'");
        }

        var config = ConfigCommand.LoadConfig(new TiaAgentLayout(_customRoot).ConfigPath);
        config.DefaultRuntime.Should().Be("opencode");
        config.Runtimes["opencode"].Mode.Should().Be("cli");
    }

    [Fact]
    public void RuntimeCommand_Use_InvalidRuntime_ReturnsError()
    {
        var useOptions = new RuntimeOptions
        {
            Subcommand = "use",
            RuntimeId = "invalid_runtime_xyz",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(useOptions, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Unknown runtime 'invalid_runtime_xyz'");
        stderr.ToString().Should().Contain("Available runtimes:");
    }

    [Fact]
    public void RuntimeCommand_Use_UnsupportedMode_ReturnsError()
    {
        var useOptions = new RuntimeOptions
        {
            Subcommand = "use",
            RuntimeId = "mimo",
            Mode = "server",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(useOptions, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Runtime 'mimo' does not support 'server' mode");
    }

    [Fact]
    public void RuntimeCommand_Doctor_RunsDiagnosticsReport()
    {
        var doctorOptions = new RuntimeOptions
        {
            Subcommand = "doctor",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(doctorOptions, stdout, stderr);

        var output = stdout.ToString();
        output.Should().Contain("TIA Agent Runtime Diagnostics");
        output.Should().Contain("Runtime Selection");
        output.Should().Contain("Executable Path");
        output.Should().Contain("Version Policy");
    }

    [Fact]
    public void RuntimeCommand_Doctor_WithJsonOption_ReturnsValidJsonReport()
    {
        var doctorOptions = new RuntimeOptions
        {
            Subcommand = "doctor",
            CustomRoot = _customRoot,
            Json = true
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(doctorOptions, stdout, stderr);

        var json = stdout.ToString();
        json.Should().Contain("\"productVersion\":");
        json.Should().Contain("\"checks\":");
        json.Should().Contain("\"summary\":");
    }

    [Fact]
    public void RuntimeCommand_UnknownSubcommand_ReturnsError()
    {
        var options = new RuntimeOptions
        {
            Subcommand = "unknown_action",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = RuntimeCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Unknown runtime subcommand 'unknown_action'");
    }

    [Fact]
    public void Program_RuntimeHelp_OutputsUsage()
    {
        using var stdout = new StringWriter();
        var currentOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            var exitCode = Program.Main(s_runtimeHelpArgs);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Usage: tia-agent runtime");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }
}
