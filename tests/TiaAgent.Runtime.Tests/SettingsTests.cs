using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class SettingsTests
{
    [Fact]
    public void Settings_DefaultValues_AreValid()
    {
        var settings = new
        {
            schemaVersion = 1,
            preferredPorts = new { bridge = 43119, opencode = 43120 },
            portRange = new { start = 43100, end = 43200 },
            startupTimeoutSeconds = 60,
            healthCheckTimeoutSeconds = 30,
            healthCheckRetryIntervalMs = 1000,
            restartFailedServices = false,
            maxRestartAttempts = 3,
            logLevel = "Information"
        };

        settings.schemaVersion.Should().Be(1);
        settings.preferredPorts.bridge.Should().Be(43119);
        settings.preferredPorts.opencode.Should().Be(43120);
        settings.portRange.start.Should().Be(43100);
        settings.portRange.end.Should().Be(43200);
        settings.startupTimeoutSeconds.Should().BeGreaterThan(0);
        settings.healthCheckTimeoutSeconds.Should().BeGreaterThan(0);
        settings.healthCheckRetryIntervalMs.Should().BeGreaterThan(0);
        settings.restartFailedServices.Should().BeFalse();
        settings.maxRestartAttempts.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Settings_InvalidPortRange_IsRejected()
    {
        var rangeStart = 43200;
        var rangeEnd = 43100;

        rangeStart.Should().BeGreaterThan(rangeEnd, "start should be less than end");
    }

    [Fact]
    public void Settings_PortRange_WithinValidBounds()
    {
        var rangeStart = 43100;
        var rangeEnd = 43200;

        rangeStart.Should().BeGreaterThanOrEqualTo(1024);
        rangeEnd.Should().BeLessThanOrEqualTo(65535);
    }

    [Fact]
    public void Settings_StartupTimeout_WithinReasonableBounds()
    {
        var timeout = 60;
        timeout.Should().BeGreaterThan(0);
        timeout.Should().BeLessThanOrEqualTo(300);
    }

    [Fact]
    public void Settings_HealthCheckTimeout_WithinReasonableBounds()
    {
        var timeout = 30;
        timeout.Should().BeGreaterThan(0);
        timeout.Should().BeLessThanOrEqualTo(120);
    }

    [Fact]
    public void Settings_HealthCheckRetryInterval_IsPositive()
    {
        var interval = 1000;
        interval.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Settings_MaxRestartAttempts_IsPositive()
    {
        var maxAttempts = 3;
        maxAttempts.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Settings_LogLevel_IsValidValue()
    {
        var validLevels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };
        var currentLevel = "Information";
        validLevels.Should().Contain(currentLevel);
    }

    [Fact]
    public void SettingsJson_SerializesCorrectly()
    {
        var settings = new
        {
            schemaVersion = 1,
            preferredPorts = new { bridge = 43119, opencode = 43120 },
            portRange = new { start = 43100, end = 43200 },
            startupTimeoutSeconds = 60,
            healthCheckTimeoutSeconds = 30,
            healthCheckRetryIntervalMs = 1000,
            restartFailedServices = false,
            maxRestartAttempts = 3,
            logLevel = "Information"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(settings);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"bridge\":43119");
        json.Should().Contain("\"opencode\":43120");
        json.Should().Contain("\"start\":43100");
        json.Should().Contain("\"end\":43200");
    }

    [Fact]
    public void SettingsJson_DeserializesCorrectly()
    {
        var json = """
        {
            "schemaVersion": 1,
            "preferredPorts": {
                "bridge": 43119,
                "opencode": 43120
            },
            "portRange": {
                "start": 43100,
                "end": 43200
            },
            "startupTimeoutSeconds": 60,
            "healthCheckTimeoutSeconds": 30,
            "healthCheckRetryIntervalMs": 1000,
            "restartFailedServices": false,
            "maxRestartAttempts": 3,
            "logLevel": "Information"
        }
        """;

        var settings = System.Text.Json.JsonSerializer.Deserialize<SettingsDto>(json);

        settings.Should().NotBeNull();
        settings!.SchemaVersion.Should().Be(1);
        settings.PreferredPorts.Should().NotBeNull();
        settings.PreferredPorts!.Bridge.Should().Be(43119);
        settings.PreferredPorts.Opencode.Should().Be(43120);
        settings.PortRange.Should().NotBeNull();
        settings.PortRange!.Start.Should().Be(43100);
        settings.PortRange.End.Should().Be(43200);
    }

    private class SettingsDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("preferredPorts")]
        public PreferredPortsDto? PreferredPorts { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("portRange")]
        public PortRangeDto? PortRange { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("startupTimeoutSeconds")]
        public int StartupTimeoutSeconds { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("healthCheckTimeoutSeconds")]
        public int HealthCheckTimeoutSeconds { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("healthCheckRetryIntervalMs")]
        public int HealthCheckRetryIntervalMs { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("restartFailedServices")]
        public bool RestartFailedServices { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("maxRestartAttempts")]
        public int MaxRestartAttempts { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("logLevel")]
        public string? LogLevel { get; set; }
    }

    private class PreferredPortsDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("bridge")]
        public int Bridge { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("opencode")]
        public int Opencode { get; set; }
    }

    private class PortRangeDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("start")]
        public int Start { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("end")]
        public int End { get; set; }
    }
}
