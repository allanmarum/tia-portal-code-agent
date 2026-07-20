using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Common;
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
