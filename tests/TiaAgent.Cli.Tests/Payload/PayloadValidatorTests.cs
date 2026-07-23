using System;
using System.IO;
using System.Text;
using FluentAssertions;
using TiaAgent.Cli.Payload;
using Xunit;

namespace TiaAgent.Cli.Tests.Payload;

public sealed class PayloadValidatorTests : IDisposable
{
    private readonly string _tempDirectory;

    public PayloadValidatorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PayloadValidatorTests_" + Guid.NewGuid().ToString("N"));
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
    public void ValidatePayload_WithValidPayload_ReturnsSuccess()
    {
        var bridgeDir = Path.Combine(_tempDirectory, "Bridge");
        Directory.CreateDirectory(bridgeDir);
        var bridgeDllPath = Path.Combine(bridgeDir, "TiaAgent.Bridge.dll");
        var bridgeContent = Encoding.UTF8.GetBytes("Fake Bridge DLL Content");
        File.WriteAllBytes(bridgeDllPath, bridgeContent);

        var bridgeHash = PayloadStore.ComputeSha256(bridgeDllPath);

        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            CommitSha = "testsha",
            Components =
            {
                ["bridge"] = new PayloadComponentMetadata
                {
                    RelativePath = "Bridge/TiaAgent.Bridge.dll",
                    Version = "0.2.0-beta.1",
                    Sha256Hash = bridgeHash,
                    SizeBytes = bridgeContent.Length
                }
            },
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "Bridge/TiaAgent.Bridge.dll",
                    Sha256Hash = bridgeHash,
                    SizeBytes = bridgeContent.Length
                }
            }
        };

        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory, expectedVersion: "0.2.0-beta.1");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Manifest.Should().NotBeNull();
        result.Manifest!.ProductVersion.Should().Be("0.2.0-beta.1");
    }

    [Fact]
    public void ValidatePayload_WithMissingDirectory_ReturnsFailure()
    {
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");
        var result = PayloadValidator.ValidatePayload(nonExistentPath);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*Payload directory does not exist*");
    }

    [Fact]
    public void ValidatePayload_WithMissingManifest_ReturnsFailure()
    {
        var result = PayloadValidator.ValidatePayload(_tempDirectory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*manifest file missing*");
    }

    [Fact]
    public void ValidatePayload_WithVersionMismatch_ReturnsFailure()
    {
        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            Files = { new PayloadFileEntry { RelativePath = "test.txt", SizeBytes = 0, Sha256Hash = "" } }
        };
        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory, expectedVersion: "0.3.0");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*product version mismatch*");
    }

    [Fact]
    public void ValidatePayload_WithMissingFile_ReturnsFailure()
    {
        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "Bridge/MissingFile.dll",
                    Sha256Hash = "abc",
                    SizeBytes = 100
                }
            }
        };
        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*Missing payload file*MissingFile.dll*");
    }

    [Fact]
    public void ValidatePayload_WithSizeMismatch_ReturnsFailure()
    {
        var filePath = Path.Combine(_tempDirectory, "file.txt");
        File.WriteAllText(filePath, "Hello World");

        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "file.txt",
                    Sha256Hash = PayloadStore.ComputeSha256(filePath),
                    SizeBytes = 99999
                }
            }
        };
        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*File size mismatch*");
    }

    [Fact]
    public void ValidatePayload_WithHashMismatch_ReturnsFailure()
    {
        var filePath = Path.Combine(_tempDirectory, "file.txt");
        File.WriteAllText(filePath, "Hello World");
        var fileInfo = new FileInfo(filePath);

        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "file.txt",
                    Sha256Hash = "0000000000000000000000000000000000000000000000000000000000000000",
                    SizeBytes = fileInfo.Length
                }
            }
        };
        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*SHA256 hash mismatch*");
    }

    [Fact]
    public void ValidatePayload_WithProhibitedSiemensAssembly_ReturnsFailure()
    {
        var siemensDllPath = Path.Combine(_tempDirectory, "Siemens.Engineering.dll");
        File.WriteAllText(siemensDllPath, "Siemens Mock");

        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            Files =
            {
                new PayloadFileEntry
                {
                    RelativePath = "Siemens.Engineering.dll",
                    Sha256Hash = PayloadStore.ComputeSha256(siemensDllPath),
                    SizeBytes = new FileInfo(siemensDllPath).Length
                }
            }
        };
        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*Prohibited Siemens runtime assembly found in payload*");
    }

    [Fact]
    public void ValidatePayload_WithEmptyFilesList_ReturnsFailure()
    {
        var manifest = new PayloadManifest
        {
            ProductVersion = "0.2.0-beta.1",
            Files = { }
        };
        PayloadStore.WriteManifest(_tempDirectory, manifest);

        var result = PayloadValidator.ValidatePayload(_tempDirectory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*does not contain any file entries*");
    }
}
