using FluentAssertions;
using TiaAgent.Cli;
using Xunit;

namespace TiaAgent.Cli.Tests;

public sealed class ChannelUtilsTests
{
    [Theory]
    [InlineData("stable", true)]
    [InlineData("rc", true)]
    [InlineData("beta", true)]
    [InlineData("alpha", true)]
    [InlineData("STABLE", true)]
    [InlineData("Beta", true)]
    [InlineData("dev", false)]
    [InlineData("nightly", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidChannel_ReturnsExpected(string? channel, bool expected)
    {
        ChannelUtils.IsValidChannel(channel).Should().Be(expected);
    }

    [Theory]
    [InlineData("stable", "stable")]
    [InlineData("RC", "rc")]
    [InlineData(" Beta ", "beta")]
    [InlineData("ALPHA", "alpha")]
    [InlineData("dev", null)]
    [InlineData("nightly", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeChannel_ReturnsNormalizedOrNull(string? input, string? expected)
    {
        ChannelUtils.NormalizeChannel(input).Should().Be(expected);
    }

    [Theory]
    // stable channel: only stable versions
    [InlineData("1.0.0", "stable", true)]
    [InlineData("1.0.0-beta.1", "stable", false)]
    [InlineData("1.0.0-rc.1", "stable", false)]
    [InlineData("1.0.0-alpha.1", "stable", false)]
    // rc channel: rc and stable
    [InlineData("1.0.0", "rc", true)]
    [InlineData("1.0.0-rc.1", "rc", true)]
    [InlineData("1.0.0-beta.1", "rc", false)]
    [InlineData("1.0.0-alpha.1", "rc", false)]
    // beta channel: beta, rc, and stable
    [InlineData("1.0.0", "beta", true)]
    [InlineData("1.0.0-rc.1", "beta", true)]
    [InlineData("1.0.0-beta.1", "beta", true)]
    [InlineData("1.0.0-alpha.1", "beta", false)]
    // alpha channel: all
    [InlineData("1.0.0", "alpha", true)]
    [InlineData("1.0.0-rc.1", "alpha", true)]
    [InlineData("1.0.0-beta.1", "alpha", true)]
    [InlineData("1.0.0-alpha.1", "alpha", true)]
    // dev versions are never eligible
    [InlineData("1.0.0-dev", "alpha", false)]
    [InlineData("1.0.0-dev", "stable", false)]
    public void IsVersionCompatibleWithChannel_ReturnsExpected(string version, string channel, bool expected)
    {
        ChannelUtils.IsVersionCompatibleWithChannel(version, channel).Should().Be(expected);
    }

    [Theory]
    [InlineData("stable", "rc", false)]    // stable -> rc is downgrade
    [InlineData("stable", "beta", false)]   // stable -> beta is downgrade
    [InlineData("stable", "alpha", false)]  // stable -> alpha is downgrade
    [InlineData("rc", "beta", false)]       // rc -> beta is downgrade
    [InlineData("rc", "alpha", false)]      // rc -> alpha is downgrade
    [InlineData("beta", "alpha", false)]    // beta -> alpha is downgrade
    [InlineData("alpha", "beta", true)]     // alpha -> beta is upgrade
    [InlineData("alpha", "rc", true)]       // alpha -> rc is upgrade
    [InlineData("alpha", "stable", true)]   // alpha -> stable is upgrade
    [InlineData("beta", "rc", true)]        // beta -> rc is upgrade
    [InlineData("beta", "stable", true)]    // beta -> stable is upgrade
    [InlineData("rc", "stable", true)]      // rc -> stable is upgrade
    [InlineData("stable", "stable", true)]  // same channel
    public void IsChannelDowngrade_ReturnsExpected(string from, string to, bool expectedIsUpgrade)
    {
        // Note: IsChannelDowngrade returns true when it IS a downgrade
        // The test data uses expectedIsUpgrade = true for upgrades, so we negate
        ChannelUtils.IsChannelDowngrade(from, to).Should().Be(!expectedIsUpgrade);
    }

    [Theory]
    [InlineData("stable", 3)]
    [InlineData("rc", 2)]
    [InlineData("beta", 1)]
    [InlineData("alpha", 0)]
    [InlineData("unknown", -1)]
    public void GetChannelPrecedence_ReturnsExpected(string channel, int expected)
    {
        ChannelUtils.GetChannelPrecedence(channel).Should().Be(expected);
    }

    [Fact]
    public void ResolveBestVersion_WithMixedVersions_SelectsCorrectForChannel()
    {
        var versions = new[] { "1.0.0-alpha.1", "1.0.0-beta.1", "1.0.0-rc.1", "1.0.0" };

        ChannelUtils.ResolveBestVersion(versions, "stable").Should().Be("1.0.0");
        ChannelUtils.ResolveBestVersion(versions, "rc").Should().Be("1.0.0");
        ChannelUtils.ResolveBestVersion(versions, "beta").Should().Be("1.0.0");
        ChannelUtils.ResolveBestVersion(versions, "alpha").Should().Be("1.0.0");
    }

    [Fact]
    public void ResolveBestVersion_WithOnlyPrereleases_SelectsCorrectForChannel()
    {
        var versions = new[] { "1.0.0-alpha.1", "1.0.0-beta.1", "1.0.0-rc.1" };

        ChannelUtils.ResolveBestVersion(versions, "stable").Should().BeNull();
        ChannelUtils.ResolveBestVersion(versions, "rc").Should().Be("1.0.0-rc.1");
        ChannelUtils.ResolveBestVersion(versions, "beta").Should().Be("1.0.0-rc.1");
        ChannelUtils.ResolveBestVersion(versions, "alpha").Should().Be("1.0.0-rc.1");
    }

    [Fact]
    public void ResolveBestVersion_WithMultipleStable_SelectsHighest()
    {
        var versions = new[] { "0.9.0", "1.0.0", "1.1.0" };

        ChannelUtils.ResolveBestVersion(versions, "stable").Should().Be("1.1.0");
    }

    [Fact]
    public void ResolveBestVersion_WithEmptyList_ReturnsNull()
    {
        ChannelUtils.ResolveBestVersion(Enumerable.Empty<string>(), "stable").Should().BeNull();
    }

    [Fact]
    public void ResolveBestVersion_WithDevVersions_ExcludesDev()
    {
        var versions = new[] { "1.0.0-dev", "0.9.0" };

        ChannelUtils.ResolveBestVersion(versions, "alpha").Should().Be("0.9.0");
        ChannelUtils.ResolveBestVersion(versions, "stable").Should().Be("0.9.0");
    }

    [Fact]
    public void IsChannelIncluded_WorksCorrectly()
    {
        // alpha includes everything
        ChannelUtils.IsChannelIncluded("alpha", "alpha").Should().BeTrue();
        ChannelUtils.IsChannelIncluded("beta", "alpha").Should().BeTrue();
        ChannelUtils.IsChannelIncluded("rc", "alpha").Should().BeTrue();
        ChannelUtils.IsChannelIncluded("stable", "alpha").Should().BeTrue();

        // stable only includes stable
        ChannelUtils.IsChannelIncluded("stable", "stable").Should().BeTrue();
        ChannelUtils.IsChannelIncluded("rc", "stable").Should().BeFalse();
        ChannelUtils.IsChannelIncluded("beta", "stable").Should().BeFalse();
        ChannelUtils.IsChannelIncluded("alpha", "stable").Should().BeFalse();

        // dev is never included
        ChannelUtils.IsChannelIncluded("dev", "alpha").Should().BeFalse();
    }
}
