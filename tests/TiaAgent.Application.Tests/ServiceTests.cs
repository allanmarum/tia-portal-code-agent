using TiaAgent.Application.Common;
using TiaAgent.Contracts.Abstractions;
using Xunit;

namespace TiaAgent.Application.Tests;

public class GuidIdGeneratorTests
{
    private readonly GuidIdGenerator _generator = new();

    [Fact]
    public void NewId_ReturnsUniqueIds()
    {
        var id1 = _generator.NewId();
        var id2 = _generator.NewId();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void NewSessionId_HasExpectedFormat()
    {
        var sessionId = _generator.NewSessionId();

        Assert.StartsWith("tia-", sessionId);
        Assert.Contains("-", sessionId);
    }

    [Fact]
    public void NewId_HasCorrectLength()
    {
        var id = _generator.NewId();

        Assert.Equal(32, id.Length); // GUID "N" format is 32 hex chars
    }
}

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentTime()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.True(now >= before);
        Assert.True(now <= after);
    }
}
