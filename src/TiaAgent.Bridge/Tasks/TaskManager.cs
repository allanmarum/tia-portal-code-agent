using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.Bridge.Tasks;

public sealed class TaskManager : IDisposable
{
    private readonly ConcurrentDictionary<string, TaskEntry> _tasks = new();
    private readonly OpenCodeClient _openCodeClient;
    private readonly int _maxConcurrentTasks;
    private int _runningCount;
    private readonly object _concurrencyLock = new();

    public TaskManager(OpenCodeClient openCodeClient, int maxConcurrentTasks)
    {
        _openCodeClient = openCodeClient;
        _maxConcurrentTasks = maxConcurrentTasks;
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
                    return;
                }
                _runningCount++;
            }

            entry.Status = BridgeTaskStatusValues.Running;
            entry.Stage = "connecting";
            entry.Message = "Connecting to OpenCode...";

            var projectKey = $"{entry.Request.Project.Id}:{entry.Request.Project.Name}";
            var prompt = BuildPrompt(entry.Request);

            var sessionId = await _openCodeClient.CreateSessionAsync(
                entry.Request.AgentId, prompt, cts.Token).ConfigureAwait(false);

            if (!sessionId.Success)
            {
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "OPENCODE_CONNECTION_FAILED",
                    Message = $"Failed to connect to OpenCode: {sessionId.RawJson}",
                    Retryable = true
                };
                return;
            }

            entry.Stage = "processing";
            entry.Message = "Agent is processing...";

            var messageResponse = await _openCodeClient.SendMessageAsync(
                sessionId.SessionId!, entry.Request.UserMessage, cts.Token).ConfigureAwait(false);

            if (!messageResponse.Success)
            {
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "OPENCODE_MESSAGE_FAILED",
                    Message = $"Agent failed: {messageResponse.RawJson}",
                    Retryable = false
                };
                return;
            }

            entry.Status = BridgeTaskStatusValues.Completed;
            entry.Stage = "completed";
            entry.Response = messageResponse.RawJson;
            entry.Message = "Task completed successfully";
        }
        catch (OperationCanceledException)
        {
            entry.Status = BridgeTaskStatusValues.Cancelled;
            entry.Message = "Task was cancelled";
        }
        catch (Exception ex)
        {
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

        return $"""
            You are a TIA Portal engineering assistant.
            Action: {action}
            CorrelationId: {request.CorrelationId}
            Project: {request.Project.Name} ({request.Project.Id})
            Selection: {request.Selection.Name} ({request.Selection.ObjectType})
            PLC: {request.Selection.PlcName}
            Language: {request.Selection.Language}

            User message: {request.UserMessage}
            """;
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
