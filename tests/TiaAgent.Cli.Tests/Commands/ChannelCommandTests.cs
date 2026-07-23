using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Cli.Tests.Commands;

public sealed class ChannelCommandTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _customRoot;

    public ChannelCommandTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ChannelCommandTests_" + Guid.NewGuid().ToString("N"));
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
    public void ChannelShow_DefaultChannel_ShowsStable()
    {
        var options = new ChannelOptions
        {
            Subcommand = "show",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Update Channel: stable");
        stdout.ToString().Should().Contain("Available channels");
    }

    [Fact]
    public void ChannelShow_WithConfiguredChannel_ShowsConfiguredChannel()
    {
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "beta" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "show",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Update Channel: beta");
    }

    [Fact]
    public void ChannelShow_JsonOutput_ReturnsValidJson()
    {
        var options = new ChannelOptions
        {
            Subcommand = "show",
            CustomRoot = _customRoot,
            Json = true
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("currentChannel").GetString().Should().Be("stable");
        doc.RootElement.GetProperty("availableChannels").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void ChannelSet_ValidChannel_Succeeds()
    {
        // Set initial channel to alpha so "beta" is an upgrade (allowed without --force)
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var initConfig = new TiaAgentConfig { UpdateChannel = "alpha" };
        ManifestStore.WriteAtomic(layout.ConfigPath, initConfig);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "beta",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("changed from 'alpha' to 'beta'");

        // Verify persisted
        var config = ManifestStore.Read<TiaAgentConfig>(layout.ConfigPath);
        config.UpdateChannel.Should().Be("beta");
    }

    [Fact]
    public void ChannelSet_SameChannel_NoOp()
    {
        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "stable",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("already set to 'stable'");
    }

    [Fact]
    public void ChannelSet_InvalidChannel_ReturnsError()
    {
        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "nightly",
            CustomRoot = _customRoot
        };

        using var stderr = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("not a valid channel");
    }

    [Fact]
    public void ChannelSet_DowngradeWithoutForce_Rejected()
    {
        // First set to stable
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "stable" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "rc",
            CustomRoot = _customRoot
        };

        using var stderr = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("channel downgrade");
    }

    [Fact]
    public void ChannelSet_DowngradeWithForce_Succeeds()
    {
        // First set to stable
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "stable" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "beta",
            CustomRoot = _customRoot,
            Force = true
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("WARNING");
        stdout.ToString().Should().Contain("downgrade");

        // Verify persisted
        var updatedConfig = ManifestStore.Read<TiaAgentConfig>(layout.ConfigPath);
        updatedConfig.UpdateChannel.Should().Be("beta");
    }

    [Fact]
    public void ChannelSet_Upgrade_SucceedsWithoutForce()
    {
        // First set to alpha
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "alpha" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "stable",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("changed from 'alpha' to 'stable'");
        stdout.ToString().Should().NotContain("WARNING");
    }

    [Fact]
    public void ChannelSet_JsonOutput_ReturnsStructuredReport()
    {
        // Set initial channel to alpha so "rc" is an upgrade (allowed without --force)
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "alpha" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "rc",
            CustomRoot = _customRoot,
            Json = true
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        var json = stdout.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("previousChannel").GetString().Should().Be("alpha");
        doc.RootElement.GetProperty("newChannel").GetString().Should().Be("rc");
    }

    [Fact]
    public void ChannelSet_CaseInsensitive_Succeeds()
    {
        // Set initial channel to alpha so "BETA" is an upgrade (allowed without --force)
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "alpha" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "BETA",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("changed from 'alpha' to 'beta'");
    }

    [Fact]
    public void ChannelSet_UnknownSubcommand_ReturnsError()
    {
        var options = new ChannelOptions
        {
            Subcommand = "invalid",
            CustomRoot = _customRoot
        };

        using var stderr = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, TextWriter.Null, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Unknown channel subcommand");
    }

    [Fact]
    public void ChannelShow_WithActiveVersion_ShowsVersionChannel()
    {
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();

        var current = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = "1.0.0-beta.1",
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivatedBy = "test"
        };
        ManifestStore.WriteAtomic(layout.CurrentManifestPath, current);

        var options = new ChannelOptions
        {
            Subcommand = "show",
            CustomRoot = _customRoot
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("1.0.0-beta.1");
        stdout.ToString().Should().Contain("beta");
    }

    [Fact]
    public void ChannelSet_Downgrade_JsonOutput_ReturnsError()
    {
        var layout = new TiaAgentLayout(_customRoot);
        layout.EnsureDirectoriesExist();
        var config = new TiaAgentConfig { UpdateChannel = "stable" };
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var options = new ChannelOptions
        {
            Subcommand = "set",
            Channel = "alpha",
            CustomRoot = _customRoot,
            Json = true
        };

        using var stdout = new StringWriter();
        var exitCode = ChannelCommand.Execute(options, stdout, TextWriter.Null);

        exitCode.Should().Be(1);
        var json = stdout.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("downgrade");
    }
}
