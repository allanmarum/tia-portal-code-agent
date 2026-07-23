using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Release;
using Xunit;

namespace TiaAgent.Cli.Tests.Release;

public sealed class ReleaseMetadataTests : IDisposable
{
    private readonly string _tempDirectory;

    public ReleaseMetadataTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ReleaseMetadataTests_" + Guid.NewGuid().ToString("N"));
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
    public void ReleaseStore_ResolveChannel_ShouldMapVersionToExpectedChannel()
    {
        ReleaseStore.ResolveChannel("0.2.0-alpha.1").Should().Be("alpha");
        ReleaseStore.ResolveChannel("0.2.0-beta.1").Should().Be("beta");
        ReleaseStore.ResolveChannel("0.2.0-rc.1").Should().Be("rc");
        ReleaseStore.ResolveChannel("0.2.0").Should().Be("stable");
        ReleaseStore.ResolveChannel("0.0.0-dev").Should().Be("dev");
        ReleaseStore.ResolveChannel("").Should().Be("dev");
    }

    [Fact]
    public void SbomGenerator_GenerateSpdxJson_ShouldProduceValidSpdxJson()
    {
        var timestamp = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero);
        var json = SbomGenerator.GenerateSpdxJson("0.2.0-beta.1", "abc1234", timestamp);

        json.Should().NotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("spdxVersion").GetString().Should().Be("SPDX-2.3");
        doc.RootElement.GetProperty("name").GetString().Should().Be("TiaAgent-0.2.0-beta.1");
        doc.RootElement.GetProperty("packages").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReleaseGenerator_And_ReleaseValidator_ShouldGenerateAndValidateReleaseMetadata()
    {
        // Setup mock release files in temp directory
        var addinFile = Path.Combine(_tempDirectory, "TiaAgent-0.2.0-beta.1.addin");
        var nupkgFile = Path.Combine(_tempDirectory, "TiaAgent.Cli.0.2.0-beta.1.nupkg");
        File.WriteAllText(addinFile, "mock addin binary content");
        File.WriteAllText(nupkgFile, "mock nupkg binary content");

        var manifest = ReleaseGenerator.GenerateReleaseMetadata(
            artifactsDirectory: _tempDirectory,
            productVersion: "0.2.0-beta.1",
            commitSha: "af09cc0",
            repoRoot: null,
            timestamp: DateTimeOffset.UtcNow);

        manifest.Should().NotBeNull();
        manifest.ProductVersion.Should().Be("0.2.0-beta.1");
        manifest.Channel.Should().Be("beta");
        manifest.CommitSha.Should().Be("af09cc0");
        manifest.Artifacts.Should().Contain(a => a.Name.EndsWith(".addin"));
        manifest.Artifacts.Should().Contain(a => a.Name.EndsWith(".nupkg"));
        manifest.Artifacts.Should().Contain(a => a.Name == "release-manifest.json");
        manifest.Artifacts.Should().Contain(a => a.Name == "SHA256SUMS");
        manifest.Artifacts.Should().Contain(a => a.Name == "sbom.spdx.json");
        manifest.Artifacts.Should().Contain(a => a.Name == "THIRD_PARTY_NOTICES.md");

        // Validate using ReleaseValidator
        var validation = ReleaseValidator.ValidateRelease(_tempDirectory, "0.2.0-beta.1");
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseValidator_WithMissingManifest_ShouldFailValidation()
    {
        var validation = ReleaseValidator.ValidateRelease(_tempDirectory, "0.2.0-beta.1");
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(e => e.Contains("release-manifest.json"));
    }

    [Fact]
    public void ReleaseValidator_WithTamperedArtifact_ShouldFailValidation()
    {
        var addinFile = Path.Combine(_tempDirectory, "TiaAgent-0.2.0-beta.1.addin");
        File.WriteAllText(addinFile, "original content");

        ReleaseGenerator.GenerateReleaseMetadata(_tempDirectory, "0.2.0-beta.1", "af09cc0");

        // Tamper with addin file after metadata generation
        File.WriteAllText(addinFile, "tampered content!");

        var validation = ReleaseValidator.ValidateRelease(_tempDirectory, "0.2.0-beta.1");
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("hash mismatch") || e.Contains("size mismatch"));
    }

    [Fact]
    public void ReleaseValidator_WithProhibitedSiemensAssembly_ShouldFailValidation()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "TiaAgent-0.2.0.addin"), "mock content");
        ReleaseGenerator.GenerateReleaseMetadata(_tempDirectory, "0.2.0", "af09cc0");

        // Add prohibited Siemens DLL
        File.WriteAllText(Path.Combine(_tempDirectory, "Siemens.Engineering.dll"), "prohibited");

        var validation = ReleaseValidator.ValidateRelease(_tempDirectory, "0.2.0");
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("Prohibited Siemens runtime assembly found"));
    }

    [Fact]
    public void VerifyReleaseCommand_ShouldReturnZeroForValidRelease()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "TiaAgent-0.2.0-rc.1.addin"), "mock addin");
        File.WriteAllText(Path.Combine(_tempDirectory, "TiaAgent.Cli.0.2.0-rc.1.nupkg"), "mock nupkg");
        ReleaseGenerator.GenerateReleaseMetadata(_tempDirectory, "0.2.0-rc.1", "1234567");

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = VerifyReleaseCommand.Execute(new VerifyReleaseOptions
        {
            Dir = _tempDirectory,
            Version = "0.2.0-rc.1"
        }, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Release validation PASSED");
    }

    [Fact]
    public void GenerateReleaseMetadataCommand_ShouldReturnZeroOnSuccess()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "TiaAgent-0.2.0-rc.1.addin"), "mock addin");
        File.WriteAllText(Path.Combine(_tempDirectory, "TiaAgent.Cli.0.2.0-rc.1.nupkg"), "mock nupkg");

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = GenerateReleaseMetadataCommand.Execute(new GenerateReleaseMetadataOptions
        {
            Dir = _tempDirectory,
            Version = "0.2.0-rc.1",
            CommitSha = "1234567"
        }, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Successfully generated release metadata");
    }
}
