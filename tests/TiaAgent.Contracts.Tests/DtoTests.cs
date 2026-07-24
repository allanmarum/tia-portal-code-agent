using TiaAgent.Contracts.Bridge;
using TiaAgent.Contracts.Common;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Contracts.Tests;

public class TiaErrorTests
{
    [Fact]
    public void TiaError_HasRequiredProperties()
    {
        var error = new TiaError
        {
            Code = TiaErrorCode.TIA_NOT_CONNECTED,
            Message = "Not connected",
            Retryable = true,
            CorrelationId = "corr-001"
        };

        Assert.Equal(TiaErrorCode.TIA_NOT_CONNECTED, error.Code);
        Assert.Equal("Not connected", error.Message);
        Assert.True(error.Retryable);
        Assert.Equal("corr-001", error.CorrelationId);
    }

    [Fact]
    public void TiaErrorException_WrapsTiaError()
    {
        var error = new TiaError
        {
            Code = TiaErrorCode.TIA_OBJECT_NOT_FOUND,
            Message = "Block not found"
        };

        var ex = new TiaErrorException(error);

        Assert.Equal(error, ex.Error);
        Assert.Equal("Block not found", ex.Message);
    }

    [Fact]
    public void TiaError_StaticFactoryMethods()
    {
        var notFound = TiaError.NotFound("Block missing", "corr-001");
        Assert.Equal(TiaErrorCode.TIA_OBJECT_NOT_FOUND, notFound.Code);
        Assert.Equal("corr-001", notFound.CorrelationId);

        var expired = TiaError.SessionExpired("corr-002");
        Assert.Equal(TiaErrorCode.TIA_SESSION_EXPIRED, expired.Code);

        var changed = TiaError.ObjectChanged("obj-1", "sha256:old", "sha256:new", "corr-003");
        Assert.Equal(TiaErrorCode.TIA_OBJECT_CHANGED, changed.Code);
        Assert.False(changed.Retryable);
    }
}

public class TiaLimitsTests
{
    [Fact]
    public void Limits_HaveReasonableValues()
    {
        Assert.True(TiaLimits.MaxPageSize > 0);
        Assert.True(TiaLimits.MaxHierarchyDepth > 0);
        Assert.True(TiaLimits.MaxHierarchyNodes > 0);
        Assert.True(TiaLimits.MaxBlockSourceBytes > 0);
        Assert.True(TiaLimits.MaxPageSize <= 1000);
    }
}

/// <summary>
/// Regression tests: all Bridge and Runtime DTOs must use { get; set; } (not { get; init; })
/// to avoid VerificationException in TIA Portal V21's partial-trust sandbox.
/// The netstandard2.0 IsExternalInit polyfill triggers the JIT verifier when called
/// from a partially-trusted Add-In assembly.
/// </summary>
public class DtoInitAccessorRegressionTests
{
    [Fact]
    public void BridgeTaskRequest_UsesSetAccessor()
    {
        // Regression: init accessor on netstandard2.0 triggers VerificationException
        // in TIA Portal's partial-trust JIT sandbox.
        var request = new BridgeTaskRequest();
        request.ContractVersion = "1.0";
        request.CorrelationId = "corr-001";
        request.Action = "explain";
        request.AgentId = "tia-explain";
        request.UserMessage = "test";

        Assert.Equal("1.0", request.ContractVersion);
        Assert.Equal("corr-001", request.CorrelationId);
    }

    [Fact]
    public void BridgeTaskRequest_ObjectInitializer_SetsAllProperties()
    {
        var request = new BridgeTaskRequest
        {
            ContractVersion = "1.0",
            CorrelationId = "corr-002",
            Action = "review",
            AgentId = "tia-review",
            UserMessage = "review this",
            TiaInstance = new TiaInstanceSnapshot { ProcessId = 1234, SessionId = "s1", Version = "21.0" },
            Project = new ProjectSnapshot { Id = "p1", Name = "Test", Path = "/test" },
            Selection = new SelectionSnapshot { Name = "OB1", ObjectType = "OB", RuntimeType = "", PlcName = "PLC1", TiaPath = "OB1", Language = "SCL" }
        };

        Assert.Equal("tia-review", request.AgentId);
        Assert.Equal(1234, request.TiaInstance.ProcessId);
        Assert.Equal("OB1", request.Selection.Name);
    }

    [Fact]
    public void BridgeTaskAccepted_UsesSetAccessor()
    {
        var accepted = new BridgeTaskAccepted
        {
            TaskId = "task-001",
            Status = "pending",
            CorrelationId = "corr-001"
        };

        Assert.Equal("task-001", accepted.TaskId);
        accepted.Status = "completed";
        Assert.Equal("completed", accepted.Status);
    }

    [Fact]
    public void BridgeTaskStatus_UsesSetAccessor()
    {
        var status = new BridgeTaskStatus
        {
            TaskId = "task-001",
            Status = "completed",
            Response = "result text"
        };

        Assert.Equal("completed", status.Status);
        status.Response = "updated";
        Assert.Equal("updated", status.Response);
    }

    [Fact]
    public void BridgeError_UsesSetAccessor()
    {
        var error = new BridgeError
        {
            Code = "TEST_ERROR",
            Message = "something failed",
            Retryable = true
        };

        Assert.Equal("TEST_ERROR", error.Code);
        error.Retryable = false;
        Assert.False(error.Retryable);
    }

    [Fact]
    public void BridgeHealthResponse_UsesSetAccessor()
    {
        var health = new BridgeHealthResponse
        {
            Status = "healthy",
            BridgeVersion = "1.0.0",
            RuntimeAvailable = true
        };

        Assert.Equal("healthy", health.Status);
        health.RuntimeAvailable = false;
        Assert.False(health.RuntimeAvailable);
    }

    [Fact]
    public void ProjectSnapshot_UsesSetAccessor()
    {
        var project = new ProjectSnapshot { Id = "p1", Name = "Test", Path = "/test" };
        Assert.Equal("Test", project.Name);
        project.Name = "Updated";
        Assert.Equal("Updated", project.Name);
    }

    [Fact]
    public void SelectionSnapshot_UsesSetAccessor()
    {
        var sel = new SelectionSnapshot { Name = "OB1", ObjectType = "OB" };
        Assert.Equal("OB1", sel.Name);
        sel.Name = "OB2";
        Assert.Equal("OB2", sel.Name);
    }

    [Fact]
    public void TiaInstanceSnapshot_UsesSetAccessor()
    {
        var tia = new TiaInstanceSnapshot { ProcessId = 100, SessionId = "s1", Version = "21.0" };
        Assert.Equal(100, tia.ProcessId);
        tia.ProcessId = 200;
        Assert.Equal(200, tia.ProcessId);
    }

    [Fact]
    public void AgentTaskRequest_UsesSetAccessor()
    {
        var request = new AgentTaskRequest
        {
            TaskId = "t1",
            CorrelationId = "c1",
            Action = "explain",
            AgentId = "a1",
            Prompt = "test prompt"
        };

        Assert.Equal("test prompt", request.Prompt);
        request.Prompt = "updated";
        Assert.Equal("updated", request.Prompt);
    }

    [Fact]
    public void AgentTaskResult_UsesSetAccessor()
    {
        var result = new AgentTaskResult
        {
            Success = true,
            Response = "result text",
            RuntimeId = "claude"
        };

        Assert.True(result.Success);
        result.Success = false;
        Assert.False(result.Success);
    }

    [Fact]
    public void AgentTaskEvent_UsesSetAccessor()
    {
        var evt = new AgentTaskEvent { EventType = "progress", Message = "processing" };
        Assert.Equal("processing", evt.Message);
        evt.Message = "done";
        Assert.Equal("done", evt.Message);
    }

    [Fact]
    public void RuntimeAvailabilityResult_UsesSetAccessor()
    {
        var result = new RuntimeAvailabilityResult { Available = true, Version = "1.0" };
        Assert.True(result.Available);
        result.Available = false;
        Assert.False(result.Available);
    }
}
