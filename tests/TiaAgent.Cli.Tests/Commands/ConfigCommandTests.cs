using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;

    public ConfigCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ConfigCommandTests_" + Guid.NewGuid().ToString("N"));
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
    public void ConfigCommand_List_DisplaysDefaultConfiguration()
    {
        var options = new ConfigOptions
        {
            Subcommand = "list",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ConfigCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Configuration File:");
        stdout.ToString().Should().Contain("Default Runtime:    opencode");
    }

    [Fact]
    public void ConfigCommand_SetAndGet_UpdatesDefaultRuntime()
    {
        var setOptions = new ConfigOptions
        {
            Subcommand = "set",
            Key = "defaultRuntime",
            Value = "mimo",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = ConfigCommand.Execute(setOptions, stdout, stderr);
            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain("Set 'defaultRuntime' to 'mimo'");
        }

        var getOptions = new ConfigOptions
        {
            Subcommand = "get",
            Key = "defaultRuntime",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = ConfigCommand.Execute(getOptions, stdout, stderr);
            exitCode.Should().Be(0);
            stdout.ToString().Trim().Should().Be("mimo");
        }
    }

    [Fact]
    public void ConfigCommand_SetRuntimeProperty_UpdatesRuntimeSettings()
    {
        var setOptions = new ConfigOptions
        {
            Subcommand = "set",
            Key = "runtimes.opencode.mode",
            Value = "server",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = ConfigCommand.Execute(setOptions, stdout, stderr);
            exitCode.Should().Be(0);
        }

        var getOptions = new ConfigOptions
        {
            Subcommand = "get",
            Key = "runtimes.opencode.mode",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = ConfigCommand.Execute(getOptions, stdout, stderr);
            exitCode.Should().Be(0);
            stdout.ToString().Trim().Should().Be("server");
        }
    }

    [Fact]
    public void ConfigCommand_Path_OutputsConfigPath()
    {
        var layout = new TiaAgentLayout(_customRoot);

        var options = new ConfigOptions
        {
            Subcommand = "path",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ConfigCommand.Execute(options, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Trim().Should().Be(layout.ConfigPath);
    }

    [Fact]
    public void ConfigCommand_Reset_RestoresDefaultConfig()
    {
        var setOptions = new ConfigOptions
        {
            Subcommand = "set",
            Key = "defaultRuntime",
            Value = "claude",
            CustomRoot = _customRoot
        };
        ConfigCommand.Execute(setOptions);

        var resetOptions = new ConfigOptions
        {
            Subcommand = "reset",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = ConfigCommand.Execute(resetOptions, stdout, stderr);
            exitCode.Should().Be(0);
        }

        var getOptions = new ConfigOptions
        {
            Subcommand = "get",
            Key = "defaultRuntime",
            CustomRoot = _customRoot
        };

        using (var stdout = new StringWriter())
        using (var stderr = new StringWriter())
        {
            var exitCode = ConfigCommand.Execute(getOptions, stdout, stderr);
            exitCode.Should().Be(0);
            stdout.ToString().Trim().Should().Be("opencode");
        }
    }
}
