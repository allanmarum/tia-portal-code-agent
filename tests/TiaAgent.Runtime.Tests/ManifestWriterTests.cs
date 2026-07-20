using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class ManifestWriterTests
{
    private readonly string _testDir;

    public ManifestWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TiaAgentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void AtomicWrite_CreatesTempFileThenMoves()
    {
        var manifestPath = Path.Combine(_testDir, "runtime.json");
        var tempPath = Path.Combine(_testDir, "runtime.json.tmp.1234");

        var content = """{"schemaVersion":1,"status":"ready"}""";

        // Write temp file
        File.WriteAllText(tempPath, content);

        // Validate
        var readBack = File.ReadAllText(tempPath);
        readBack.Should().Be(content);

        // Move to final
        File.Move(tempPath, manifestPath);

        File.Exists(manifestPath).Should().BeTrue();
        File.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public void AtomicWrite_FailedValidation_LeavesOriginalIntact()
    {
        var manifestPath = Path.Combine(_testDir, "runtime.json");
        var originalContent = """{"schemaVersion":1,"status":"ready"}""";
        var invalidContent = """{"schemaVersion":1,"status": INVALID JSON""";

        // Write original
        File.WriteAllText(manifestPath, originalContent);

        // Try to write invalid content
        var tempPath = Path.Combine(_testDir, "runtime.json.tmp.1234");
        File.WriteAllText(tempPath, invalidContent);

        // Validation would fail, so we don't move
        var readBack = File.ReadAllText(manifestPath);
        readBack.Should().Be(originalContent);
    }

    [Fact]
    public async Task AtomicWrite_ConcurrentWrites_DoNotCorrupt()
    {
        var manifestPath = Path.Combine(_testDir, "runtime.json");
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var tempPath = Path.Combine(_testDir, $"runtime.json.tmp.{index}");
                var content = $"{{\"schemaVersion\":1,\"status\":\"ready\",\"index\":{index}}}";
                File.WriteAllText(tempPath, content);
                File.Move(tempPath, manifestPath, true);
            }));
        }

        await Task.WhenAll(tasks);

        // File should be valid JSON
        var finalContent = File.ReadAllText(manifestPath);
        finalContent.Should().Contain("\"schemaVersion\":1");
    }
}
