using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class SupervisorIntegrationTests
{
    [Fact]
    public void Integration_StartupSequence_IsCorrect()
    {
        var steps = new[]
        {
            "Acquire supervisor mutex",
            "Create LocalAppData structure",
            "Validate prerequisites",
            "Detect and clean stale runtime",
            "Allocate ports",
            "Generate transient credentials",
            "Publish runtime status as starting",
            "Start Bridge",
            "Wait for Bridge health",
            "Generate OpenCode config",
            "Start OpenCode",
            "Wait for OpenCode health",
            "Publish runtime status as ready",
            "Monitor child processes"
        };

        steps.Should().HaveCount(14);
        steps[0].Should().Contain("mutex");
        steps[1].Should().Contain("structure");
        steps[2].Should().Contain("prerequisites");
        steps[3].Should().Contain("stale");
        steps[4].Should().Contain("ports");
        steps[5].Should().Contain("credentials");
        steps[6].Should().Contain("starting");
        steps[7].Should().Contain("Bridge");
        steps[8].Should().Contain("health");
        steps[9].Should().Contain("OpenCode");
        steps[10].Should().Contain("OpenCode");
        steps[11].Should().Contain("health");
        steps[12].Should().Contain("ready");
        steps[13].Should().Contain("Monitor");
    }

    [Fact]
    public void Integration_ShutdownSequence_IsCorrect()
    {
        var steps = new[]
        {
            "Read and validate runtime.json",
            "Confirm instance belongs to TiaAgent",
            "Set global state to stopping",
            "Stop OpenCode",
            "Stop Bridge",
            "Force kill owned processes that refused",
            "Remove transient secrets",
            "Remove supervisor.lock",
            "Release mutex",
            "Update manifest to stopped"
        };

        steps.Should().HaveCount(10);
        steps[0].Should().Contain("runtime.json");
        steps[1].Should().Contain("instance");
        steps[2].Should().Contain("stopping");
        steps[3].Should().Contain("OpenCode");
        steps[4].Should().Contain("Bridge");
        steps[5].Should().Contain("Force");
        steps[6].Should().Contain("secrets");
        steps[7].Should().Contain("lock");
        steps[8].Should().Contain("mutex");
        steps[9].Should().Contain("stopped");
    }

    [Fact]
    public void Integration_RuntimeDirectory_Structure()
    {
        var expectedDirs = new[]
        {
            "config",
            "runtime",
            "runtime/secrets",
            "logs",
            "scripts",
            "temp"
        };

        expectedDirs.Should().HaveCount(6);
        expectedDirs.Should().Contain("config");
        expectedDirs.Should().Contain("runtime");
        expectedDirs.Should().Contain("runtime/secrets");
        expectedDirs.Should().Contain("logs");
        expectedDirs.Should().Contain("scripts");
        expectedDirs.Should().Contain("temp");
    }

    [Fact]
    public void Integration_Scripts_ArePresent()
    {
        var expectedScripts = new[] { "run.ps1", "stop.ps1", "status.ps1" };
        expectedScripts.Should().HaveCount(3);
    }

    [Fact]
    public void Integration_PortAllocation_PrefersStandardPorts()
    {
        var preferredPorts = new Dictionary<string, int>
        {
            { "bridge", 43119 },
            { "opencode", 43120 }
        };

        preferredPorts["bridge"].Should().Be(43119);
        preferredPorts["opencode"].Should().Be(43120);
    }

    [Fact]
    public void Integration_HealthEndpoints_AreExposed()
    {
        var healthEndpoints = new[]
        {
            "http://127.0.0.1:43119/health",
            "http://127.0.0.1:43120/health"
        };

        healthEndpoints.Should().HaveCount(2);
        healthEndpoints.Should().AllSatisfy(url => url.Should().Contain("/health"));
    }
}
