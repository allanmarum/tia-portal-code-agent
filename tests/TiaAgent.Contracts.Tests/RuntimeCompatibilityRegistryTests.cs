using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Contracts.Tests;

public class RuntimeCompatibilityRegistryTests
{
    [Fact]
    public void IsKnownRuntime_ValidRuntimes_ReturnsTrue()
    {
        Assert.True(RuntimeCompatibilityRegistry.IsKnownRuntime("opencode"));
        Assert.True(RuntimeCompatibilityRegistry.IsKnownRuntime("mimo"));
        Assert.True(RuntimeCompatibilityRegistry.IsKnownRuntime("claude"));
        Assert.True(RuntimeCompatibilityRegistry.IsKnownRuntime("OPENCODE"));
    }

    [Fact]
    public void IsKnownRuntime_InvalidRuntimes_ReturnsFalse()
    {
        Assert.False(RuntimeCompatibilityRegistry.IsKnownRuntime("invalid_xyz"));
        Assert.False(RuntimeCompatibilityRegistry.IsKnownRuntime(""));
        Assert.False(RuntimeCompatibilityRegistry.IsKnownRuntime(null));
    }

    [Fact]
    public void GetMetadata_KnownRuntimes_ReturnsMetadata()
    {
        var opencodeMeta = RuntimeCompatibilityRegistry.GetMetadata("opencode");
        Assert.NotNull(opencodeMeta);
        Assert.Equal("OpenCode", opencodeMeta.DisplayName);
        Assert.Contains("server", opencodeMeta.SupportedModes);

        var mimoMeta = RuntimeCompatibilityRegistry.GetMetadata("mimo");
        Assert.NotNull(mimoMeta);
        Assert.Equal("Mimo CLI", mimoMeta.DisplayName);
        Assert.Contains("cli", mimoMeta.SupportedModes);
    }

    [Fact]
    public void ExtractSemVer_VariousFormats_ExtractsVersion()
    {
        Assert.Equal("1.2.3", RuntimeCompatibilityRegistry.ExtractSemVer("opencode v1.2.3"));
        Assert.Equal("0.2.1", RuntimeCompatibilityRegistry.ExtractSemVer("claude 0.2.1-beta.1"));
        Assert.Equal("1.0.0", RuntimeCompatibilityRegistry.ExtractSemVer("1.0.0"));
        Assert.Null(RuntimeCompatibilityRegistry.ExtractSemVer("no version here"));
    }

    [Fact]
    public void IsVersionSupported_VersionComparison_WorksAsExpected()
    {
        Assert.True(RuntimeCompatibilityRegistry.IsVersionSupported("1.0.0", "0.1.0", out var parsed1));
        Assert.Equal("1.0.0", parsed1);

        Assert.True(RuntimeCompatibilityRegistry.IsVersionSupported("0.1.0", "0.1.0", out var parsed2));
        Assert.Equal("0.1.0", parsed2);

        Assert.False(RuntimeCompatibilityRegistry.IsVersionSupported("0.0.5", "0.1.0", out var parsed3));
        Assert.Equal("0.0.5", parsed3);
    }
}
