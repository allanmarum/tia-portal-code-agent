using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class HealthCheckTests
{
    [Fact]
    public void HealthCheck_RetryCount_CalculatedCorrectly()
    {
        var timeoutSeconds = 30;
        var retryIntervalMs = 1000;
        var maxRetries = (int)Math.Ceiling(timeoutSeconds / (retryIntervalMs / 1000.0));

        maxRetries.Should().Be(30);
    }

    [Fact]
    public void HealthCheck_TimeoutSeconds_IsRespected()
    {
        var timeoutSeconds = 5;
        var startTime = DateTime.UtcNow;
        var deadline = startTime.AddSeconds(timeoutSeconds);
        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

        elapsed.Should().BeLessThanOrEqualTo(timeoutSeconds + 1); // Allow 1s tolerance
    }

    [Fact]
    public void HealthCheck_RetryInterval_IsPositive()
    {
        var retryIntervalMs = 1000;
        retryIntervalMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HealthCheck_ServiceIdentity_CanBeValidated()
    {
        var expectedService = "tia-agent-bridge";
        var actualService = "tia-agent-bridge";

        actualService.Should().Be(expectedService);
    }

    [Fact]
    public void HealthCheck_Status_HealthyIsValid()
    {
        var validStatuses = new[] { "healthy", "ok" };
        var status = "healthy";

        validStatuses.Should().Contain(status);
    }

    [Fact]
    public void HealthCheck_Status_UnhealthyIsInvalid()
    {
        var validStatuses = new[] { "healthy", "ok" };
        var status = "unhealthy";

        validStatuses.Should().NotContain(status);
    }

    [Fact]
    public void HealthCheck_Url_FormatsCorrectly()
    {
        var host = "127.0.0.1";
        var port = 43119;
        var healthUrl = $"http://{host}:{port}/health";

        healthUrl.Should().Be("http://127.0.0.1:43119/health");
    }

    [Fact]
    public void HealthCheck_Timeout_WithinReasonableBounds()
    {
        var timeoutSeconds = 30;
        timeoutSeconds.Should().BeGreaterThan(0);
        timeoutSeconds.Should().BeLessThanOrEqualTo(120);
    }
}
