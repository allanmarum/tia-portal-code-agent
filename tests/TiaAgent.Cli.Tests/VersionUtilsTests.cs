using FluentAssertions;
using TiaAgent.Cli;
using Xunit;

namespace TiaAgent.Cli.Tests;

public sealed class VersionUtilsTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("0.1.0", "0.2.0", -1)]
    [InlineData("0.2.0", "0.1.0", 1)]
    [InlineData("0.2.0-beta.1", "0.2.0-rc.1", -1)]
    [InlineData("0.2.0-rc.1", "0.2.0", -1)]
    [InlineData("0.2.0", "0.2.0-beta.1", 1)]
    [InlineData("v1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    public void Compare_ReturnsExpectedOrdering(string v1, string v2, int expected)
    {
        var result = VersionUtils.Compare(v1, v2);
        int sign = result < 0 ? -1 : (result > 0 ? 1 : 0);
        sign.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(null, "1.0.0", -1)]
    [InlineData("1.0.0", null, 1)]
    [InlineData("", "", 0)]
    [InlineData("", "1.0.0", -1)]
    public void Compare_NullAndEmpty_HandledGracefully(string? v1, string? v2, int expected)
    {
        var result = VersionUtils.Compare(v1, v2);
        int sign = result < 0 ? -1 : (result > 0 ? 1 : 0);
        sign.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0-beta.1", "1.0.0-beta.1", 0)]
    [InlineData("1.0.0-beta.1+sha.abc", "1.0.0-beta.1+sha.def", 0)]
    [InlineData("1.0.0-beta.1", "1.0.0-beta.2", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    public void Compare_PrereleaseAndBuildMetadata_HandledCorrectly(string v1, string v2, int expected)
    {
        var result = VersionUtils.Compare(v1, v2);
        int sign = result < 0 ? -1 : (result > 0 ? 1 : 0);
        sign.Should().Be(expected);
    }
}
