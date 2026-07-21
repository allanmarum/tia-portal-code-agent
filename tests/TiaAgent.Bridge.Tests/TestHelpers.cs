using System;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Tests;

/// <summary>
/// Fake runtime adapter for testing. Always reports as available.
/// </summary>
public sealed class FakeRuntime : IAgentRuntime
{
    public string Id { get; }
    public string DisplayName { get; }
    public bool ShouldFail { get; init; }
    public string? FailureError { get; init; }
    public string? FailureErrorCode { get; init; }

    public FakeRuntime(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public Task<RuntimeAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        if (ShouldFail)
        {
            return Task.FromResult(new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = "fake",
                Error = FailureError ?? "Simulated failure"
            });
        }

        return Task.FromResult(new RuntimeAvailabilityResult
        {
            Available = true,
            Executable = "fake",
            Version = "1.0.0-test",
            Mode = "cli"
        });
    }

    public Task<AgentTaskResult> ExecuteAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken)
    {
        if (ShouldFail)
        {
            return Task.FromResult(new AgentTaskResult
            {
                Success = false,
                Error = FailureError ?? "Simulated execution failure",
                ErrorCode = FailureErrorCode ?? "RUNTIME_TASK_FAILED",
                RuntimeId = Id
            });
        }

        progress?.Report(new AgentTaskEvent { EventType = "progress", Message = "Processing..." });

        return Task.FromResult(new AgentTaskResult
        {
            Success = true,
            Response = $"Fake response for task {request.TaskId}",
            RuntimeId = Id,
            RuntimeMode = "cli"
        });
    }

    public Task CancelAsync(string taskId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fake runtime that tracks disposal.
/// </summary>
public sealed class FakeDisposableRuntime : IAgentRuntime, IDisposable
{
    public string Id { get; }
    public string DisplayName { get; }
    public bool WasDisposed { get; private set; }

    public FakeDisposableRuntime(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public Task<RuntimeAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken)
        => Task.FromResult(new RuntimeAvailabilityResult { Available = true });

    public Task<AgentTaskResult> ExecuteAsync(AgentTaskRequest request, IProgress<AgentTaskEvent>? progress, CancellationToken cancellationToken)
        => Task.FromResult(new AgentTaskResult { Success = true, RuntimeId = Id });

    public Task CancelAsync(string taskId, CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => WasDisposed = true;
}
