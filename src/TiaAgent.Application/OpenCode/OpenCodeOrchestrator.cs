using Microsoft.Extensions.Logging;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Errors;

namespace TiaAgent.Application.OpenCode;

/// <summary>
/// Orchestrates the full Add-In → OpenCode → MCP roundtrip.
/// Creates sessions, starts tasks, watches events, and collects results.
/// </summary>
public class OpenCodeOrchestrator : IOpenCodeOrchestrator
{
    private readonly IOpenCodeClient _client;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly ILogger<OpenCodeOrchestrator> _logger;

    public OpenCodeOrchestrator(
        IOpenCodeClient client,
        IIdGenerator idGenerator,
        IClock clock,
        ILogger<OpenCodeOrchestrator> logger)
    {
        _client = client;
        _idGenerator = idGenerator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> IsOpenCodeAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _client.HealthCheckAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenCode health check failed");
            return false;
        }
    }

    public async Task<OpenCodeOrchestratorResult> ExecuteTaskAsync(
        OpenCodeTaskDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var startTime = _clock.UtcNow;
        var toolCalls = new List<ToolCallRecord>();

        _logger.LogInformation(
            "Starting OpenCode task {Action} (correlationId={CorrelationId})",
            descriptor.Action, descriptor.CorrelationId);

        // Step 1: Check OpenCode availability
        var isAvailable = await IsOpenCodeAvailableAsync(cancellationToken);
        if (!isAvailable)
        {
            return new OpenCodeOrchestratorResult
            {
                Success = false,
                Error = "OpenCode agent runtime is not available. Ensure OpenCode is running on the configured address.",
                ErrorCode = TiaErrorCode.OPENCODE_UNAVAILABLE.ToString(),
                CorrelationId = descriptor.CorrelationId,
                Duration = _clock.UtcNow - startTime
            };
        }

        try
        {
            // Step 2: Create session
            _logger.LogDebug("Creating OpenCode session (correlationId={CorrelationId})", descriptor.CorrelationId);
            var session = await _client.CreateSessionAsync(
                new CreateOpenCodeSessionRequest
                {
                    CorrelationId = descriptor.CorrelationId,
                    TiaSessionId = descriptor.TiaSessionId,
                    ProjectId = descriptor.ProjectId,
                    DefaultAgent = descriptor.AgentId
                },
                cancellationToken);

            // Step 3: Start task
            _logger.LogDebug("Starting OpenCode task in session {SessionId}", session.SessionId);
            var task = await _client.StartTaskAsync(
                new StartOpenCodeTaskRequest
                {
                    SessionId = session.SessionId,
                    CorrelationId = descriptor.CorrelationId,
                    AgentId = descriptor.AgentId ?? "tia-explain",
                    Message = descriptor.Message,
                    SelectionToken = descriptor.SelectionToken
                },
                cancellationToken);

            // Step 4: Watch events and collect result
            _logger.LogDebug("Watching events for task {TaskId}", task.TaskId);
            string? finalResponse = null;
            var completed = false;

            await foreach (var evt in _client.WatchTaskAsync(task.TaskId, cancellationToken))
            {
                _logger.LogDebug(
                    "Event: {EventType} - {Message} (task={TaskId})",
                    evt.EventType, evt.Message, evt.TaskId);

                switch (evt.EventType)
                {
                    case "tool_call":
                        toolCalls.Add(new ToolCallRecord
                        {
                            ToolName = evt.Message ?? "unknown",
                            Success = true
                        });
                        break;

                    case "tool_error":
                        if (toolCalls.Count > 0)
                        {
                            var last = toolCalls[toolCalls.Count - 1];
                            toolCalls[toolCalls.Count - 1] = new ToolCallRecord
                            {
                                ToolName = last.ToolName,
                                Success = false,
                                Error = evt.Message,
                                Duration = last.Duration
                            };
                        }
                        break;

                    case "completed":
                        finalResponse = evt.Message;
                        completed = true;
                        break;

                    case "failed":
                        return new OpenCodeOrchestratorResult
                        {
                            Success = false,
                            Error = evt.Message ?? "OpenCode task failed",
                            ErrorCode = TiaErrorCode.OPENCODE_TASK_FAILED.ToString(),
                            CorrelationId = descriptor.CorrelationId,
                            SessionId = session.SessionId,
                            TaskId = task.TaskId,
                            ToolCalls = toolCalls,
                            Duration = _clock.UtcNow - startTime
                        };
                }

                if (completed) break;
            }

            // Check if cancellation was requested (some transports swallow OperationCanceledException internally)
            cancellationToken.ThrowIfCancellationRequested();

            return new OpenCodeOrchestratorResult
            {
                Success = completed,
                Response = finalResponse,
                Error = completed ? null : "OpenCode task completed without a final response",
                ErrorCode = completed ? null : TiaErrorCode.OPENCODE_TASK_FAILED.ToString(),
                CorrelationId = descriptor.CorrelationId,
                SessionId = session.SessionId,
                TaskId = task.TaskId,
                ToolCalls = toolCalls,
                Duration = _clock.UtcNow - startTime
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OpenCode task cancelled (correlationId={CorrelationId})", descriptor.CorrelationId);
            return new OpenCodeOrchestratorResult
            {
                Success = false,
                Error = "Task was cancelled",
                ErrorCode = TiaErrorCode.TIA_CANCELLED.ToString(),
                CorrelationId = descriptor.CorrelationId,
                Duration = _clock.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenCode task failed (correlationId={CorrelationId})", descriptor.CorrelationId);
            return new OpenCodeOrchestratorResult
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}",
                ErrorCode = TiaErrorCode.OPENCODE_TASK_FAILED.ToString(),
                CorrelationId = descriptor.CorrelationId,
                Duration = _clock.UtcNow - startTime
            };
        }
    }
}
