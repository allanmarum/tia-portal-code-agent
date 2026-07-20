using TiaAgent.Application.Common;
using Xunit;

namespace TiaAgent.Application.Tests;

public class CorrelationContextTests
{
    [Fact]
    public void CurrentCorrelationId_DefaultsToNone()
    {
        var context = new CorrelationContext();

        Assert.Equal("none", context.CurrentCorrelationId);
    }

    [Fact]
    public void SetCorrelationId_SetsValue()
    {
        var context = new CorrelationContext();

        using var scope = context.SetCorrelationId("corr-001");

        Assert.Equal("corr-001", context.CurrentCorrelationId);
    }

    [Fact]
    public void SetCorrelationId_RestoresPreviousOnDispose()
    {
        var context = new CorrelationContext();

        using (var scope = context.SetCorrelationId("corr-001"))
        {
            Assert.Equal("corr-001", context.CurrentCorrelationId);
        }

        Assert.Equal("none", context.CurrentCorrelationId);
    }

    [Fact]
    public void SetCorrelationId_NestedScopes()
    {
        var context = new CorrelationContext();

        using (var outer = context.SetCorrelationId("outer"))
        {
            Assert.Equal("outer", context.CurrentCorrelationId);

            using (var inner = context.SetCorrelationId("inner"))
            {
                Assert.Equal("inner", context.CurrentCorrelationId);
            }

            Assert.Equal("outer", context.CurrentCorrelationId);
        }

        Assert.Equal("none", context.CurrentCorrelationId);
    }

    [Fact]
    public async Task SetCorrelationId_PropagatesAcrossAsync()
    {
        var context = new CorrelationContext();
        using var scope = context.SetCorrelationId("async-corr");

        await Task.Run(() =>
        {
            Assert.Equal("async-corr", context.CurrentCorrelationId);
        });
    }
}
