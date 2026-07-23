using System;
using System.IO;
using FluentAssertions;
using TiaAgent.Cli.Payload;
using Xunit;

namespace TiaAgent.Cli.Tests.Payload;

public sealed class PayloadManifestTests : IDisposable
{
    private readonly string _tempDirectory;

    public PayloadManifestTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PayloadManifestTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void PayloadManifest_DefaultValues_ShouldMatchSchema()
    {
        var manifest = new PayloadManifest();

        manifest.SchemaVersion.Should().Be(1);
        manifest.ProductVersion.Should().BeEmpty();
        manifest.CommitSha.Should().Be("unknown");
        manifest.Compatibility.TiaPortalVersion.Should().Be("V21");
        manifest.Compatibility.OpennessVersion.Should().Be("V21");
        manifest.Compatibility.TargetFramework.Should().Be("net8.0");
        manifest.Components.Should().BeEmpty();
        manifest.Files.Should().BeEmpty();
    }

    [Fact]
    public void PayloadStore_WriteAndReadManifest_ShouldRoundtripCorrectly()
    {
        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            CommitSha = "abc1234",
            Compatibility = new PayloadCompatibilityMetadata
            {
                TiaPortalVersion = "V21",
                OpennessVersion = "V21",
                TargetFramework = "net8.0"
            },
            Components =
            {
                ["bridge"] = new PayloadComponentMetadata
                {
                    RelativePath = "Bridge/TiaAgent.Bridge.dll",
                    Version = "0.2.0-beta.1",
                    Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    SizeBytes = 1024
                }
            },
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "Bridge/TiaAgent.Bridge.dll",
                    Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    SizeBytes = 1024
                }
            }
        };

        PayloadStore.WriteManifest(_tempDirectory, manifest);

        File.Exists(Path.Combine(_tempDirectory, PayloadStore.ManifestFileName)).Should().BeTrue();

        var loaded = PayloadStore.ReadManifest(_tempDirectory);
        loaded.Should().NotBeNull();
        loaded.SchemaVersion.Should().Be(1);
        loaded.ProductVersion.Should().Be("0.2.0-beta.1");
        loaded.CommitSha.Should().Be("abc1234");
        loaded.Components.Should().ContainKey("bridge");
        loaded.Components["bridge"].RelativePath.Should().Be("Bridge/TiaAgent.Bridge.dll");
        loaded.Files.Should().HaveCount(1);
        loaded.Files[0].RelativePath.Should().Be("Bridge/TiaAgent.Bridge.dll");
    }

    [Fact]
    public void PayloadLocator_GetBundledPayloadDirectory_ShouldResolvePaths()
    {
        var customPath = _tempDirectory;
        var subPayload = Path.Combine(customPath, "payload");
        Directory.CreateDirectory(subPayload);

        var resolved = PayloadLocator.GetBundledPayloadDirectory(customPath);
        resolved.Should().Be(subPayload);
    }
}
