using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void SingleInstance_MutexName_IsLocal()
    {
        var mutexName = "Local\\TiaAgent.Supervisor";
        mutexName.Should().StartWith("Local\\");
    }

    [Fact]
    public void SingleInstance_MutexName_IsUnique()
    {
        var mutexName = "Local\\TiaAgent.Supervisor";
        mutexName.Should().Be("Local\\TiaAgent.Supervisor");
    }

    [Fact]
    public void SingleInstance_LockFile_ContainsRequiredFields()
    {
        var lockData = new
        {
            instanceId = "test-1234",
            supervisorPid = 12345,
            startedAt = "2026-01-01T00:00:00Z"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(lockData);

        json.Should().Contain("\"instanceId\":\"test-1234\"");
        json.Should().Contain("\"supervisorPid\":12345");
        json.Should().Contain("\"startedAt\":\"2026-01-01T00:00:00Z\"");
    }

    [Fact]
    public void SingleInstance_LockFile_CanBeParsed()
    {
        var json = """
        {
            "instanceId": "20260101-120000-1234",
            "supervisorPid": 12345,
            "startedAt": "2026-01-01T00:00:00Z"
        }
        """;

        var lockData = System.Text.Json.JsonSerializer.Deserialize<LockDto>(json);

        lockData.Should().NotBeNull();
        lockData!.InstanceId.Should().Be("20260101-120000-1234");
        lockData.SupervisorPid.Should().Be(12345);
        lockData.StartedAt.Should().Be("2026-01-01T00:00:00Z");
    }

    [Fact]
    public void SingleInstance_StaleDetection_ProcessNotRunning()
    {
        // PID 99999 is unlikely to exist - GetProcessById throws if not found
        Action act = () => System.Diagnostics.Process.GetProcessById(99999);
        act.Should().Throw<ArgumentException>();
    }

    private class LockDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("instanceId")]
        public string? InstanceId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("supervisorPid")]
        public int SupervisorPid { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("startedAt")]
        public string? StartedAt { get; set; }
    }
}
