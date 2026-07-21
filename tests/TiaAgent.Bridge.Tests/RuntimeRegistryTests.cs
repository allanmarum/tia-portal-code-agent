using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Bridge.Tests;

public class RuntimeRegistryTests
{
    private readonly BridgeLogger _logger = new();

    [Fact]
    public void Register_AndGetRuntime_ReturnsRegisteredRuntime()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "test" };
        var registry = new RuntimeRegistry(config, _logger);
        var runtime = new FakeRuntime("test", "Test Runtime");

        registry.Register(runtime);

        var result = registry.GetRuntime("test");
        result.Should().BeSameAs(runtime);
        result.Id.Should().Be("test");
        result.DisplayName.Should().Be("Test Runtime");
    }

    [Fact]
    public void GetRuntime_UnknownId_ThrowsWithActionableMessage()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "opencode" };
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        registry.Register(new FakeRuntime("opencode", "OpenCode"));

        var act = () => registry.GetRuntime("nonexistent");

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Unknown runtime 'nonexistent'")
            .And.Contain("Available runtimes:")
            .And.Contain("mimo")
            .And.Contain("opencode");
    }

    [Fact]
    public void ResolveRuntime_RequestOverride_TakesPrecedence()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "opencode" };
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        registry.Register(new FakeRuntime("opencode", "OpenCode"));
        registry.Register(new FakeRuntime("claude", "Claude Code CLI"));

        var result = registry.ResolveRuntime("claude");

        result.Id.Should().Be("claude");
    }

    [Fact]
    public void ResolveRuntime_EnvVar_TakesPrecedenceOverConfig()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "opencode" };
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        registry.Register(new FakeRuntime("opencode", "OpenCode"));

        Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", "mimo");
        try
        {
            var result = registry.ResolveRuntime(null);
            result.Id.Should().Be("mimo");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", null);
        }
    }

    [Fact]
    public void ResolveRuntime_ConfigDefault_UsedWhenNoOverrideOrEnv()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "mimo" };
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        registry.Register(new FakeRuntime("opencode", "OpenCode"));

        Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", null);
        var result = registry.ResolveRuntime(null);

        result.Id.Should().Be("mimo");
    }

    [Fact]
    public void ResolveRuntime_HardcodedDefault_UsedWhenConfigIsEmpty()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "" };
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("opencode", "OpenCode"));

        Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", null);
        var result = registry.ResolveRuntime(null);

        result.Id.Should().Be("opencode");
    }

    [Fact]
    public void ResolveRuntime_UnknownRuntime_ThrowsWithAvailableList()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "opencode" };
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));

        var act = () => registry.ResolveRuntime("claude");

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Unknown runtime 'claude'");
    }

    [Fact]
    public void GetAllRuntimes_ReturnsAllRegistered()
    {
        var config = new TiaAgentConfig();
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        registry.Register(new FakeRuntime("opencode", "OpenCode"));
        registry.Register(new FakeRuntime("claude", "Claude Code CLI"));

        var all = registry.GetAllRuntimes();

        all.Should().HaveCount(3);
    }

    [Fact]
    public void GetDefaultRuntimeId_ReturnsConfigDefault()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "claude" };
        var registry = new RuntimeRegistry(config, _logger);

        Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", null);
        registry.GetDefaultRuntimeId().Should().Be("claude");
    }

    [Fact]
    public void GetDefaultRuntimeId_EnvVarOverridesConfig()
    {
        var config = new TiaAgentConfig { DefaultRuntime = "opencode" };
        var registry = new RuntimeRegistry(config, _logger);

        Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", "mimo");
        try
        {
            registry.GetDefaultRuntimeId().Should().Be("mimo");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIA_AGENT_RUNTIME", null);
        }
    }

    [Fact]
    public async Task CheckAllAvailabilityAsync_ReturnsResultsForAllRuntimes()
    {
        var config = new TiaAgentConfig();
        var registry = new RuntimeRegistry(config, _logger);
        registry.Register(new FakeRuntime("mimo", "Mimo CLI"));
        registry.Register(new FakeRuntime("opencode", "OpenCode"));

        var results = await registry.CheckAllAvailabilityAsync(CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().ContainKey("mimo");
        results.Should().ContainKey("opencode");
        results["mimo"].Available.Should().BeTrue();
        results["opencode"].Available.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DisposesAllRuntimes()
    {
        var config = new TiaAgentConfig();
        var registry = new RuntimeRegistry(config, _logger);
        var disposable = new FakeDisposableRuntime("test", "Test");
        registry.Register(disposable);

        registry.Dispose();

        disposable.WasDisposed.Should().BeTrue();
    }
}
