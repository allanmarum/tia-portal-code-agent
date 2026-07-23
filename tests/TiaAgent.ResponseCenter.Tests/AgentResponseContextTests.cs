using FluentAssertions;
using TiaAgent.ResponseCenter.Models;
using Xunit;

namespace TiaAgent.ResponseCenter.Tests;

public class AgentResponseContextTests
{
    [Fact]
    public void ActionDisplay_MapsKnownActions()
    {
        var explain = new AgentResponseContext
        {
            TaskId = "t1", BridgeUrl = "http://localhost", Action = "explain"
        };
        explain.ActionDisplay.Should().Be("Explain selected object");

        var review = new AgentResponseContext
        {
            TaskId = "t1", BridgeUrl = "http://localhost", Action = "review"
        };
        review.ActionDisplay.Should().Be("Review selected object");

        var propose = new AgentResponseContext
        {
            TaskId = "t1", BridgeUrl = "http://localhost", Action = "propose"
        };
        propose.ActionDisplay.Should().Be("Propose changes");
    }

    [Fact]
    public void ActionDisplay_FallsBackForUnknownAction()
    {
        var ctx = new AgentResponseContext
        {
            TaskId = "t1", BridgeUrl = "http://localhost", Action = "custom"
        };
        ctx.ActionDisplay.Should().Be("Action: custom");
    }

    [Fact]
    public void Context_StoresSelectionInfo()
    {
        var ctx = new AgentResponseContext
        {
            TaskId = "t1",
            BridgeUrl = "http://localhost:43119",
            Action = "explain",
            ObjectName = "FB_Conveyor",
            ObjectType = "Function Block",
            PlcName = "PLC_1",
            ProjectName = "MyProject",
            CorrelationId = "tia-abc123"
        };

        ctx.ObjectName.Should().Be("FB_Conveyor");
        ctx.ObjectType.Should().Be("Function Block");
        ctx.PlcName.Should().Be("PLC_1");
        ctx.ProjectName.Should().Be("MyProject");
        ctx.CorrelationId.Should().Be("tia-abc123");
    }

    [Fact]
    public void Context_DefaultsOptionalFields()
    {
        var ctx = new AgentResponseContext
        {
            TaskId = "t1", BridgeUrl = "http://localhost", Action = "explain"
        };

        ctx.ObjectName.Should().Be("");
        ctx.ObjectType.Should().Be("");
        ctx.PlcName.Should().BeNull();
        ctx.ProjectName.Should().BeNull();
        ctx.CorrelationId.Should().BeNull();
        ctx.AuthToken.Should().BeNull();
    }

    [Fact]
    public void Context_IsImmutable()
    {
        var ctx = new AgentResponseContext
        {
            TaskId = "t1", BridgeUrl = "http://localhost", Action = "explain"
        };

        // Records are immutable — with-expressions create new instances
        var ctx2 = ctx with { ObjectName = "NewName" };
        ctx.ObjectName.Should().Be("");
        ctx2.ObjectName.Should().Be("NewName");
    }
}
