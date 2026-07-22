using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TiaAgent.OpenCode.Client;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class OpenCodeProcessManagerTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var manager = new OpenCodeProcessManager("dotnet", "--version", "http://127.0.0.1:43120");
        manager.InstanceId.Should().NotBeNullOrEmpty();
        manager.ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task IsRunningAsync_WhenNoProcessStarted_ReturnsFalse()
    {
        using var manager = new OpenCodeProcessManager("dotnet", "--version", "http://127.0.0.1:43120");
        var isRunning = await manager.IsRunningAsync(CancellationToken.None);
        isRunning.Should().BeFalse();
    }

    [Fact]
    public async Task HealthCheckAsync_UnreachableHost_ReturnsFalse()
    {
        using var manager = new OpenCodeProcessManager("dotnet", "--version", "http://127.0.0.1:59999");
        var isHealthy = await manager.HealthCheckAsync(CancellationToken.None);
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        using var manager = new OpenCodeProcessManager("dotnet", "--version", "http://127.0.0.1:43120");
        Func<Task> act = () => manager.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
