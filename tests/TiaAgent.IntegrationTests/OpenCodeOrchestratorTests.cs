using TiaAgent.Application.Common;
using TiaAgent.Application.OpenCode;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Errors;
using TiaAgent.OpenCode.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace TiaAgent.IntegrationTests;

public class OpenCodeOrchestratorTests
{
    private static OpenCodeOrchestrator CreateOrchestrator(MockOpenCodeClient? client = null)
    {
        return new OpenCodeOrchestrator(
            client ?? new MockOpenCodeClient(),
            new GuidIdGenerator(),
            new SystemClock(),
            NullLogger<OpenCodeOrchestrator>.Instance);
    }

    private static OpenCodeTaskDescriptor CreateDescriptor(string action = "test_connection", string? correlationId = null)
    {
        return new OpenCodeTaskDescriptor
        {
            Action = action,
            Message = "Return exactly: OPENCODE_CONNECTION_OK",
            CorrelationId = correlationId ?? $"corr-{Guid.NewGuid():N}",
            TiaSessionId = "tia-sim-001",
            ProjectId = "sim-project-001"
        };
    }

    [Fact]
    public async Task IsOpenCodeAvailable_WithHealthyClient_ReturnsTrue()
    {
        var orchestrator = CreateOrchestrator();

        var available = await orchestrator.IsOpenCodeAvailableAsync(CancellationToken.None);

        Assert.True(available);
    }

    [Fact]
    public async Task IsOpenCodeAvailable_WithUnhealthyClient_ReturnsFalse()
    {
        var client = new MockOpenCodeClient { HealthCheckResult = false };
        var orchestrator = CreateOrchestrator(client);

        var available = await orchestrator.IsOpenCodeAvailableAsync(CancellationToken.None);

        Assert.False(available);
    }

    [Fact]
    public async Task ExecuteTask_WithWorkingClient_ReturnsSuccess()
    {
        var orchestrator = CreateOrchestrator();
        var descriptor = CreateDescriptor();

        var result = await orchestrator.ExecuteTaskAsync(descriptor, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Equal(descriptor.CorrelationId, result.CorrelationId);
        Assert.NotNull(result.SessionId);
        Assert.NotNull(result.TaskId);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteTask_WithWorkingClient_CapturesToolCalls()
    {
        var orchestrator = CreateOrchestrator();
        var descriptor = CreateDescriptor();

        var result = await orchestrator.ExecuteTaskAsync(descriptor, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ToolCalls);
    }

    [Fact]
    public async Task ExecuteTask_WhenOpenCodeUnavailable_ReturnsError()
    {
        var client = new MockOpenCodeClient { HealthCheckResult = false };
        var orchestrator = CreateOrchestrator(client);
        var descriptor = CreateDescriptor();

        var result = await orchestrator.ExecuteTaskAsync(descriptor, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TiaErrorCode.OPENCODE_UNAVAILABLE.ToString(), result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteTask_WithCancellation_ReturnsCancelledError()
    {
        var client = new MockOpenCodeClient { DelayMs = 10000 };
        var orchestrator = CreateOrchestrator(client);
        var descriptor = CreateDescriptor();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await orchestrator.ExecuteTaskAsync(descriptor, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(TiaErrorCode.TIA_CANCELLED.ToString(), result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteTask_PropagatesCorrelationId()
    {
        var orchestrator = CreateOrchestrator();
        var correlationId = "test-corr-12345";
        var descriptor = CreateDescriptor(correlationId: correlationId);

        var result = await orchestrator.ExecuteTaskAsync(descriptor, CancellationToken.None);

        Assert.Equal(correlationId, result.CorrelationId);
    }
}
