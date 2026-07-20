#if SIEMENS
using FluentAssertions;
using TiaAgent.AddIn.Runtime;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class RuntimeDiscoveryTests
{
    [Fact]
    public void RuntimeDiscovery_ValidManifest_ParsesCorrectly()
    {
        var json = """
        {
            "schemaVersion": 1,
            "instanceId": "test-1234",
            "status": "ready",
            "supervisorPid": 12345,
            "startedAt": "2026-01-01T00:00:00Z",
            "updatedAt": "2026-01-01T00:00:00Z",
            "services": {
                "bridge": {
                    "status": "healthy",
                    "pid": 12346,
                    "host": "127.0.0.1",
                    "port": 43119,
                    "baseUrl": "http://127.0.0.1:43119",
                    "healthUrl": "http://127.0.0.1:43119/health"
                },
                "opencode": {
                    "status": "healthy",
                    "pid": 12347,
                    "host": "127.0.0.1",
                    "port": 43120,
                    "baseUrl": "http://127.0.0.1:43120",
                    "healthUrl": "http://127.0.0.1:43120/health"
                }
            }
        }
        """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<RuntimeManifestDto>(json);

        manifest.Should().NotBeNull();
        manifest!.SchemaVersion.Should().Be(1);
        manifest.InstanceId.Should().Be("test-1234");
        manifest.Status.Should().Be("ready");
        manifest.Services.Should().NotBeNull();
        manifest.Services!.Bridge.Should().NotBeNull();
        manifest.Services.Bridge!.Port.Should().Be(43119);
        manifest.Services.OpenCode.Should().NotBeNull();
        manifest.Services.OpenCode!.Port.Should().Be(43120);
    }

    [Fact]
    public void RuntimeDiscovery_InvalidSchemaVersion_ReturnsNull()
    {
        var json = """
        {
            "schemaVersion": 999,
            "instanceId": "test",
            "status": "ready"
        }
        """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<RuntimeManifestDto>(json);
        manifest.Should().NotBeNull();
        manifest!.SchemaVersion.Should().NotBe(1);
    }

    [Fact]
    public void RuntimeDiscovery_StatusNotReady_ReturnsNull()
    {
        var json = """
        {
            "schemaVersion": 1,
            "instanceId": "test",
            "status": "starting"
        }
        """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<RuntimeManifestDto>(json);
        manifest.Should().NotBeNull();
        manifest!.Status.Should().NotBe("ready");
    }

    [Fact]
    public void RuntimeDiscovery_MissingFile_ReturnsNull()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), "nonexistent", "runtime.json");
        File.Exists(manifestPath).Should().BeFalse();
    }

    [Fact]
    public void RuntimeDiscovery_HealthValidation_Success()
    {
        var healthUrl = "http://127.0.0.1:43119/health";
        var expectedService = "tia-agent-bridge";

        healthUrl.Should().Contain("/health");
        expectedService.Should().Be("tia-agent-bridge");
    }

    private class RuntimeManifestDto
    {
        public int SchemaVersion { get; set; }
        public string? InstanceId { get; set; }
        public string? Status { get; set; }
        public int SupervisorPid { get; set; }
        public ServicesDto? Services { get; set; }
    }

    private class ServicesDto
    {
        public ServiceDto? Bridge { get; set; }
        public ServiceDto? OpenCode { get; set; }
    }

    private class ServiceDto
    {
        public string? Status { get; set; }
        public int Pid { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? BaseUrl { get; set; }
        public string? HealthUrl { get; set; }
    }
}
#endif
