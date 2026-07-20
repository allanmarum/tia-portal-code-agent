using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class RuntimeManifestTests
{
    [Fact]
    public void ManifestSchema_Version1_IsCurrentVersion()
    {
        var schemaVersion = 1;
        schemaVersion.Should().Be(1);
    }

    [Fact]
    public void ManifestStatus_AllValidStates()
    {
        var validGlobalStates = new[] { "starting", "ready", "degraded", "stopping", "stopped", "failed" };
        var validServiceStates = new[] { "pending", "starting", "healthy", "unhealthy", "stopped", "failed" };

        validGlobalStates.Should().HaveCount(6);
        validGlobalStates.Should().Contain("ready");
        validGlobalStates.Should().Contain("failed");

        validServiceStates.Should().HaveCount(6);
        validServiceStates.Should().Contain("healthy");
        validServiceStates.Should().Contain("unhealthy");
    }

    [Fact]
    public void ManifestJson_SerializesCorrectly()
    {
        var manifest = new
        {
            schemaVersion = 1,
            instanceId = "test-1234",
            status = "ready",
            supervisorPid = 1234,
            startedAt = "2026-01-01T00:00:00Z",
            updatedAt = "2026-01-01T00:00:00Z",
            services = new
            {
                bridge = new
                {
                    status = "healthy",
                    pid = 1235,
                    host = "127.0.0.1",
                    port = 43119,
                    baseUrl = "http://127.0.0.1:43119",
                    healthUrl = "http://127.0.0.1:43119/health"
                },
                opencode = new
                {
                    status = "healthy",
                    pid = 1236,
                    host = "127.0.0.1",
                    port = 43120,
                    baseUrl = "http://127.0.0.1:43120",
                    healthUrl = "http://127.0.0.1:43120/health"
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"instanceId\":\"test-1234\"");
        json.Should().Contain("\"status\":\"ready\"");
        json.Should().Contain("\"port\":43119");
        json.Should().Contain("\"port\":43120");
    }

    [Fact]
    public void ManifestJson_DeserializesCorrectly()
    {
        var json = """
        {
            "schemaVersion": 1,
            "instanceId": "test-1234",
            "status": "ready",
            "supervisorPid": 1234,
            "startedAt": "2026-01-01T00:00:00Z",
            "updatedAt": "2026-01-01T00:00:00Z",
            "services": {
                "bridge": {
                    "status": "healthy",
                    "pid": 1235,
                    "host": "127.0.0.1",
                    "port": 43119,
                    "baseUrl": "http://127.0.0.1:43119",
                    "healthUrl": "http://127.0.0.1:43119/health"
                },
                "opencode": {
                    "status": "healthy",
                    "pid": 1236,
                    "host": "127.0.0.1",
                    "port": 43120,
                    "baseUrl": "http://127.0.0.1:43120",
                    "healthUrl": "http://127.0.0.1:43120/health"
                }
            }
        }
        """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<ManifestDto>(json);

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
    public void ManifestJson_InvalidSchemaVersion_IsInvalid()
    {
        var json = """
        {
            "schemaVersion": 999,
            "instanceId": "test",
            "status": "ready"
        }
        """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<ManifestDto>(json);
        manifest.Should().NotBeNull();
        manifest!.SchemaVersion.Should().NotBe(1);
    }

    [Fact]
    public void ManifestJson_MissingInstanceId_IsInvalid()
    {
        var json = """
        {
            "schemaVersion": 1,
            "status": "ready"
        }
        """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<ManifestDto>(json);
        manifest.Should().NotBeNull();
        manifest!.InstanceId.Should().BeNull();
    }

    private class ManifestDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("instanceId")]
        public string? InstanceId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("supervisorPid")]
        public int SupervisorPid { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("startedAt")]
        public string? StartedAt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("services")]
        public ServicesDto? Services { get; set; }
    }

    private class ServicesDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("bridge")]
        public ServiceDto? Bridge { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("opencode")]
        public ServiceDto? OpenCode { get; set; }
    }

    private class ServiceDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pid")]
        public int Pid { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("host")]
        public string? Host { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("port")]
        public int Port { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("baseUrl")]
        public string? BaseUrl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("healthUrl")]
        public string? HealthUrl { get; set; }
    }
}
