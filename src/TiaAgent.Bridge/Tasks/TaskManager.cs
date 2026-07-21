using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Contracts.Bridge;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Tasks;

public sealed class TaskManager : IDisposable
{
    private readonly ConcurrentDictionary<string, TaskEntry> _tasks = new();
    private readonly RuntimeRegistry _runtimeRegistry;
    private readonly int _maxConcurrentTasks;
    private readonly BridgeLogger _logger;
    private int _runningCount;
    private readonly object _concurrencyLock = new();

    public TaskManager(RuntimeRegistry runtimeRegistry, int maxConcurrentTasks, BridgeLogger logger)
    {
        _runtimeRegistry = runtimeRegistry;
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
            Error = entry.Error,
            RuntimeId = entry.RuntimeId,
            RuntimeVersion = entry.RuntimeVersion
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

        // Also tell the runtime to cancel
        if (!string.IsNullOrEmpty(entry.RuntimeId))
        {
            try
            {
                var runtime = _runtimeRegistry.GetRuntime(entry.RuntimeId);
                _ = runtime.CancelAsync(taskId, CancellationToken.None);
            }
            catch { }
        }

        return true;
    }

    private async Task ExecuteTaskAsync(TaskEntry entry, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        entry.CancellationTokenSource = cts;

        try
        {
            _logger.Info($"Task {entry.TaskId}: starting execution (action={entry.Request.Action}, agent={entry.Request.AgentId}, runtime={entry.Request.Runtime ?? "default"})");

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
            entry.Stage = "resolving_runtime";
            entry.Message = "Resolving runtime...";

            // Resolve which runtime to use
            IAgentRuntime runtime;
            try
            {
                runtime = _runtimeRegistry.ResolveRuntime(entry.Request.Runtime);
            }
            catch (InvalidOperationException ex)
            {
                _logger.Warn($"Task {entry.TaskId}: runtime resolution failed: {ex.Message}");
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "RUNTIME_NOT_FOUND",
                    Message = ex.Message,
                    Retryable = false
                };
                return;
            }

            entry.RuntimeId = runtime.Id;
            _logger.Info($"Task {entry.TaskId}: resolved to runtime '{runtime.Id}' ({runtime.DisplayName})");

            // Check availability
            entry.Stage = "checking_availability";
            entry.Message = $"Checking {runtime.DisplayName} availability...";

            var availability = await runtime.CheckAvailabilityAsync(cts.Token).ConfigureAwait(false);
            if (!availability.Available)
            {
                var availableRuntimes = _runtimeRegistry.GetAllRuntimes();
                var availableList = string.Join(", ", availableRuntimes.Select(r => r.Id));

                _logger.Warn($"Task {entry.TaskId}: runtime '{runtime.Id}' is unavailable: {availability.Error}");
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = "RUNTIME_UNAVAILABLE",
                    Message = $"Selected runtime `{runtime.Id}` is unavailable.\n"
                            + $"Executable: {availability.Executable ?? "unknown"}\n"
                            + $"Reason: {availability.Error ?? "unknown"}\n"
                            + $"Available runtimes: {availableList}",
                    Retryable = false
                };
                return;
            }

            entry.RuntimeVersion = availability.Version;

            // Build prompt
            entry.Stage = "building_prompt";
            entry.Message = "Building prompt...";

            var prompt = BuildPrompt(entry.Request);

            // Create the runtime task request
            var runtimeRequest = new AgentTaskRequest
            {
                TaskId = entry.TaskId,
                CorrelationId = entry.CorrelationId,
                Action = entry.Request.Action,
                AgentId = entry.Request.AgentId,
                Prompt = prompt,
                RuntimeOverride = entry.Request.Runtime,
                Project = entry.Request.Project,
                Selection = entry.Request.Selection
            };

            // Execute
            entry.Stage = "processing";
            entry.Message = $"Executing via {runtime.DisplayName}...";

            var progress = new Progress<AgentTaskEvent>(evt =>
            {
                entry.Stage = evt.EventType;
                if (!string.IsNullOrEmpty(evt.Message))
                    entry.Message = evt.Message;
            });

            var result = await runtime.ExecuteAsync(
                runtimeRequest, progress, cts.Token).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.Info($"Task {entry.TaskId}: completed successfully via {runtime.Id}");
                entry.Status = BridgeTaskStatusValues.Completed;
                entry.Stage = "completed";
                entry.Response = result.Response;
                entry.Message = "Task completed successfully";
            }
            else
            {
                _logger.Warn($"Task {entry.TaskId}: runtime execution failed: {result.Error}");
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = result.ErrorCode ?? "RUNTIME_TASK_FAILED",
                    Message = result.Error ?? "Unknown runtime error",
                    Retryable = result.ErrorCode == "TASK_TIMEOUT" || result.ErrorCode == "RUNTIME_UNAVAILABLE"
                };
            }
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
        public string? RuntimeId { get; set; }
        public string? RuntimeVersion { get; set; }
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
        public string? RuntimeId { get; init; }
        public string? RuntimeVersion { get; init; }
    }
}
