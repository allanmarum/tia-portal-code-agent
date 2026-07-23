using FluentAssertions;
using TiaAgent.ResponseCenter.ViewModels;
using Xunit;

namespace TiaAgent.ResponseCenter.Tests;

public class ErrorSanitizationTests
{
    [Fact]
    public void Sanitize_RemovesBearerTokens()
    {
        var input = "Request failed: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abc123";
        var result = ErrorDetailsViewModel.Sanitize(input);
        result.Should().NotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void Sanitize_RemovesTokenQueryParams()
    {
        var input = "GET http://localhost:43119/v1/tasks?token=secret123&key=apikey456";
        var result = ErrorDetailsViewModel.Sanitize(input);
        result.Should().NotContain("secret123");
        result.Should().NotContain("apikey456");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void Sanitize_RemovesPasswordValues()
    {
        var input = "Connection failed: password=mySecretPass host=localhost";
        var result = ErrorDetailsViewModel.Sanitize(input);
        result.Should().NotContain("mySecretPass");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void Sanitize_RemovesEnvVarSecrets()
    {
        var input = "API_TOKEN=sk-abc123def456 DATABASE_PASSWORD=p@ssw0rd";
        var result = ErrorDetailsViewModel.Sanitize(input);
        result.Should().NotContain("sk-abc123def456");
        result.Should().NotContain("p@ssw0rd");
    }

    [Fact]
    public void Sanitize_PreservesNormalText()
    {
        var input = "The task timed out after 300 seconds. Please try again.";
        var result = ErrorDetailsViewModel.Sanitize(input);
        result.Should().Be(input);
    }

    [Fact]
    public void Sanitize_HandlesNullInput()
    {
        ErrorDetailsViewModel.Sanitize(null!).Should().BeNull();
    }

    [Fact]
    public void Sanitize_HandlesEmptyInput()
    {
        ErrorDetailsViewModel.Sanitize("").Should().Be("");
    }

    [Fact]
    public void FromBridgeError_CreatesViewModelWithSanitizedDetails()
    {
        var vm = ErrorDetailsViewModel.FromBridgeError(
            "Task failed",
            "OPENCODE_TASK_FAILED",
            "Bearer token123 leaked in error",
            true,
            "tia-abc123");

        vm.UserMessage.Should().Be("Task failed");
        vm.TechnicalDetails.Should().NotContain("token123");
        vm.TechnicalDetails.Should().Contain("[REDACTED]");
        vm.Retryable.Should().BeTrue();
        vm.CorrelationId.Should().Be("tia-abc123");
    }

    [Fact]
    public void FromBridgeError_MapsKnownErrorCodes()
    {
        var vm = ErrorDetailsViewModel.FromBridgeError(null, "BRIDGE_BUSY", null, true, null);
        vm.UserMessage.Should().Contain("currently processing");
    }

    [Fact]
    public void FromBridgeError_DefaultsUnknownErrorCode()
    {
        var vm = ErrorDetailsViewModel.FromBridgeError(null, "UNKNOWN_CODE", null, false, null);
        vm.UserMessage.Should().Contain("unexpected error");
    }
}
