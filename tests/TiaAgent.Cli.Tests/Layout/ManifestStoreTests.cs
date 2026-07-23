using System;
using System.IO;
using FluentAssertions;
using TiaAgent.Cli.Layout;
using Xunit;

namespace TiaAgent.Cli.Tests.Layout;

public class ManifestStoreTests : IDisposable
{
    private readonly string _tempDirectory;

    public ManifestStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiaAgentTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Layout_Paths_ShouldBeSubdirectoriesOfRoot()
    {
        var layout = new TiaAgentLayout(_tempDirectory);

        layout.RootPath.Should().Be(_tempDirectory);
        layout.VersionsPath.Should().Be(Path.Combine(_tempDirectory, "versions"));
        layout.ConfigPath.Should().Be(Path.Combine(_tempDirectory, "config.json"));
        layout.CurrentManifestPath.Should().Be(Path.Combine(_tempDirectory, "current.json"));
        layout.InstallationsManifestPath.Should().Be(Path.Combine(_tempDirectory, "installations.json"));
        layout.LogsPath.Should().Be(Path.Combine(_tempDirectory, "logs"));
        layout.RuntimePath.Should().Be(Path.Combine(_tempDirectory, "runtime"));
        layout.CachePath.Should().Be(Path.Combine(_tempDirectory, "cache"));
    }

    [Fact]
    public void Layout_EnsureDirectoriesExist_ShouldCreateAllDirectories()
    {
        var layout = new TiaAgentLayout(_tempDirectory);

        layout.EnsureDirectoriesExist();

        Directory.Exists(layout.RootPath).Should().BeTrue();
        Directory.Exists(layout.VersionsPath).Should().BeTrue();
        Directory.Exists(layout.LogsPath).Should().BeTrue();
        Directory.Exists(layout.RuntimePath).Should().BeTrue();
        Directory.Exists(layout.CachePath).Should().BeTrue();
    }

    [Fact]
    public void ManifestStore_WriteAtomicAndRead_ShouldPersistDataCorrectly()
    {
        var manifestPath = Path.Combine(_tempDirectory, "current.json");
        var manifest = new CurrentManifest
        {
            SchemaVersion = 1,
            ActiveVersion = "0.2.0-beta.1",
            ActivatedBy = "cli-test",
        };

        ManifestStore.WriteAtomic(manifestPath, manifest);

        File.Exists(manifestPath).Should().BeTrue();

        var loaded = ManifestStore.Read<CurrentManifest>(manifestPath);
        loaded.Should().NotBeNull();
        loaded.SchemaVersion.Should().Be(1);
        loaded.ActiveVersion.Should().Be("0.2.0-beta.1");
        loaded.ActivatedBy.Should().Be("cli-test");
    }

    [Fact]
    public void ManifestStore_ReadMissingFile_ShouldReturnNewDefaultInstance()
    {
        var manifestPath = Path.Combine(_tempDirectory, "nonexistent.json");

        var loaded = ManifestStore.Read<CurrentManifest>(manifestPath);

        loaded.Should().NotBeNull();
        loaded.ActiveVersion.Should().BeEmpty();
    }

    [Fact]
    public void ManifestStore_ReadMalformedFile_ShouldThrowInvalidDataException()
    {
        var manifestPath = Path.Combine(_tempDirectory, "corrupt.json");
        File.WriteAllText(manifestPath, "{ invalid json format: ");

        Action act = () => ManifestStore.Read<CurrentManifest>(manifestPath);

        act.Should().Throw<InvalidDataException>()
           .WithMessage("*malformed JSON*");
    }

    [Fact]
    public void ManifestStore_ReadEmptyFile_ShouldThrowInvalidDataException()
    {
        var manifestPath = Path.Combine(_tempDirectory, "empty.json");
        File.WriteAllText(manifestPath, "   ");

        Action act = () => ManifestStore.Read<CurrentManifest>(manifestPath);

        act.Should().Throw<InvalidDataException>()
           .WithMessage("*is empty*");
    }
}
