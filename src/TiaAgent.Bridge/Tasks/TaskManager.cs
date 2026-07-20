using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.Bridge.Tasks;

public sealed class TaskManager : IDisposable
{
    private readonly ConcurrentDictionary<string, TaskEntry> _tasks = new();
    private readonly OpenCodeClient _openCodeClient;
    private readonly int _maxConcurrentTasks;
    private readonly BridgeLogger _logger;
    private int _runningCount;
    private readonly object _concurrencyLock = new();

    public TaskManager(OpenCodeClient openCodeClient, int maxConcurrentTasks, BridgeLogger logger)
    {
        _openCodeClient = openCodeClient;
        _maxConcurrentTasks = maxConcurrentTasks;
        _logger = logger;
    }

    public int ActiveTaskCount => Volatile.Read(ref _runningCount);

    public string CreateTaskAsync(BridgeTaskRequest request, CancellationToken cancellationToken = default)
    {
        var taskId = Guid.NewGuid().ToString("N")[..12];
        var entry = new TaskEntry
        {
            TaskId = taskId,
            CorrelationId = request.CorrelationId,
            Status = BridgeTaskStatusValues.Pending,
            Request = request,
            CreatedAt = DateTime.UtcNow
        };

        _tasks[taskId] = entry;

        // Fire-and-forget background execution
        _ = ExecuteTaskAsync(entry, cancellationToken);

        return taskId;
    }

    public TaskStatusResponse? GetTaskStatus(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var entry))
            return null;

        return new TaskStatusResponse
        {
            TaskId = entry.TaskId,
            Status = entry.Status,
            Stage = entry.Stage,
            Message = entry.Message,
            Response = entry.Response,
            Error = entry.Error
        };
    }

    public bool CancelTask(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var entry))
            return false;

        if (entry.Status == BridgeTaskStatusValues.Completed ||
            entry.Status == BridgeTaskStatusValues.Failed ||
            entry.Status == BridgeTaskStatusValues.Cancelled)
            return false;

        entry.Status = BridgeTaskStatusValues.Cancelled;
        entry.Message = "Task cancelled by user";
        entry.CancellationTokenSource?.Cancel();
        return true;
    }

    private async Task ExecuteTaskAsync(TaskEntry entry, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        entry.CancellationTokenSource = cts;

        try
        {
            _logger.Info($"Task {entry.TaskId}: starting execution (action={entry.Request.Action}, agent={entry.Request.AgentId})");

            lock (_concurrencyLock)
            {
                if (_runningCount >= _maxConcurrentTasks)
                {
                    entry.Status = BridgeTaskStatusValues.Failed;
                    entry.Error = new BridgeError
                    {
                        Code = "BRIDGE_BUSY",
                        Message = $"Max concurrent tasks ({_maxConcurrentTasks}) reached",
                        Retryable = true
                    };
                    _logger.Warn($"Task {entry.TaskId}: rejected — max concurrent tasks reached");
                    return;
                }
                _runningCount++;
            }

            entry.Status = BridgeTaskStatusValues.Running;
            entry.Stage = "connecting";
            entry.Message = "Connecting to OpenCode...";

            if (entry.Request.Project == null)
            {
                _logger.Warn($"Task {entry.TaskId}: Request.Project is null");
            }
            if (entry.Request.Selection == null)
            {
                _logger.Warn($"Task {entry.TaskId}: Request.Selection is null");
            }

            var prompt = BuildPrompt(entry.Request);

            _logger.Info($"Task {entry.TaskId}: calling OpenCode CreateSessionAsync");
            var sessionId = await _openCodeClient.CreateSessionAsync(
                entry.Request.AgentId, prompt, cts.Token).ConfigureAwait(false);

            if (!sessionId.Success)
            {
                _logger.Warn($"Task {entry.TaskId}: OpenCode session creation failed: {sessionId.RawJson}");
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "OPENCODE_CONNECTION_FAILED",
                    Message = $"Failed to connect to OpenCode: {sessionId.RawJson}",
                    Retryable = true
                };
                return;
            }

            _logger.Info($"Task {entry.TaskId}: session created (id={sessionId.SessionId}), sending message");
            entry.Stage = "processing";
            entry.Message = "Agent is processing...";

            if (string.IsNullOrEmpty(sessionId.SessionId))
            {
                _logger.Warn($"Task {entry.TaskId}: OpenCode returned null/empty sessionId");
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "OPENCODE_SESSION_FAILED",
                    Message = "OpenCode returned no session ID",
                    Retryable = true
                };
                return;
            }

            var messageResponse = await _openCodeClient.SendMessageAsync(
                sessionId.SessionId, entry.Request.UserMessage, cts.Token).ConfigureAwait(false);

            if (!messageResponse.Success)
            {
                _logger.Warn($"Task {entry.TaskId}: OpenCode message failed: {messageResponse.RawJson}");
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "OPENCODE_MESSAGE_FAILED",
                    Message = $"Agent failed: {messageResponse.RawJson}",
                    Retryable = false
                };
                return;
            }

            _logger.Info($"Task {entry.TaskId}: completed successfully");
            entry.Status = BridgeTaskStatusValues.Completed;
            entry.Stage = "completed";
            entry.Response = messageResponse.RawJson;
            entry.Message = "Task completed successfully";
        }
        catch (OperationCanceledException)
        {
            _logger.Info($"Task {entry.TaskId}: cancelled");
            entry.Status = BridgeTaskStatusValues.Cancelled;
            entry.Message = "Task was cancelled";
        }
        catch (Exception ex)
        {
            _logger.Error($"Task {entry.TaskId}: failed with exception", ex);
            entry.Status = BridgeTaskStatusValues.Failed;
            entry.Error = new BridgeError
            {
                Code = "BRIDGE_INTERNAL_ERROR",
                Message = ex.Message,
                Retryable = false
            };
        }
        finally
        {
            Interlocked.Decrement(ref _runningCount);
            entry.CompletedAt = DateTime.UtcNow;
        }
    }

    private static string BuildPrompt(BridgeTaskRequest request)
    {
        var action = request.Action switch
        {
            BridgeActions.Explain => "explain",
            BridgeActions.Review => "review",
            BridgeActions.Propose => "propose",
            _ => request.Action
        };

        var projectName = request.Project?.Name ?? "Unknown";
        var projectId = request.Project?.Id ?? "unknown";
        var selectionName = request.Selection?.Name ?? "Unknown";
        var selectionType = request.Selection?.ObjectType ?? "Unknown";
        var plcName = request.Selection?.PlcName ?? "Unknown";
        var language = request.Selection?.Language ?? "Unknown";

        return $"You are a TIA Portal engineering assistant.\n"
             + $"Action: {action}\n"
             + $"CorrelationId: {request.CorrelationId}\n"
             + $"Project: {projectName} ({projectId})\n"
             + $"Selection: {selectionName} ({selectionType})\n"
             + $"PLC: {plcName}\n"
             + $"Language: {language}\n"
             + $"\n"
             + $"User message: {request.UserMessage}";
    }

    public void Dispose()
    {
        foreach (var kvp in _tasks)
        {
            kvp.Value.CancellationTokenSource?.Cancel();
            kvp.Value.CancellationTokenSource?.Dispose();
        }
        _tasks.Clear();
    }

    private sealed class TaskEntry
    {
        public string TaskId { get; init; } = null!;
        public string CorrelationId { get; init; } = null!;
        public string Status { get; set; } = null!;
        public string? Stage { get; set; }
        public string? Message { get; set; }
        public string? Response { get; set; }
        public BridgeError? Error { get; set; }
        public BridgeTaskRequest Request { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
        public DateTime? CompletedAt { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
    }

    public sealed class TaskStatusResponse
    {
        public string TaskId { get; init; } = null!;
        public string Status { get; init; } = null!;
        public string? Stage { get; init; }
        public string? Message { get; init; }
        public string? Response { get; init; }
        public BridgeError? Error { get; init; }
    }
}
