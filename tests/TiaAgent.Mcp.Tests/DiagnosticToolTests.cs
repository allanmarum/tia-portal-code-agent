using TiaAgent.Application.Hashing;
using TiaAgent.Application.Common;
using TiaAgent.Mcp.Tools;
using TiaAgent.Simulator;
using Xunit;

namespace TiaAgent.Mcp.Tests;

public class TiaDiagnosticToolsTests
{
    [Fact]
    public async Task Ping_WithConnectedProject_ReturnsOk()
    {
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock());
        var tools = new TiaDiagnosticTools(service);

        var result = await tools.Ping(CancellationToken.None);

        Assert.Equal("ok", result.Status);
        Assert.Equal("tia-addin", result.Source);
        Assert.True(result.TiaConnected);
        Assert.True(result.ProjectOpen);
        Assert.Equal("DemoConveyorLine", result.ProjectName);
        Assert.Equal("V21", result.TiaVersion);
    }

    [Fact]
    public async Task Ping_WhenDisconnected_ReturnsDegraded()
    {
        var config = new SimulatorScenarioConfig { SimulateDisconnected = true };
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock(), config);
        var tools = new TiaDiagnosticTools(service);

        var result = await tools.Ping(CancellationToken.None);

        Assert.Equal("degraded", result.Status);
        Assert.False(result.TiaConnected);
        Assert.False(result.ProjectOpen);
    }
}
