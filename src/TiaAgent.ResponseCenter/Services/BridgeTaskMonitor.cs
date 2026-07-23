using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Bridge;
using TiaAgent.ResponseCenter.Models;

namespace TiaAgent.ResponseCenter.Services;

/// <summary>
/// Monitors a Bridge task by polling its status endpoint.
/// Runs on a background thread, marshaling state changes to the caller via callbacks.
/// Handles network errors, timeouts, malformed responses, and clean shutdown.
/// </summary>
public sealed class BridgeTaskMonitor : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly AgentResponseContext _context;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _taskTimeout;
    private Task? _pollingTask;

    /// <summary>Raised on any state change. Always invoked on a background thread.</summary>
    public event Action<AgentTaskState, string?, string?>? StateChanged;

    /// <summary>Raised when the task completes with a response.</summary>
    public event Action<string>? ResponseReceived;

    /// <summary>Raised when the task fails with error details.</summary>
    public event Action<string?, string?, string?, bool>? ErrorOccurred;

    /// <summary>Raised when polling encounters transient errors.</summary>
    public event Action<string>? PollingError;

    public BridgeTaskMonitor(
        AgentResponseContext context,
        TimeSpan? pollingInterval = null,
        TimeSpan? taskTimeout = null,
        HttpClient? httpClient = null)
    {
        _context = context;
        _pollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(500);
        _taskTimeout = taskTimeout ?? TimeSpan.FromMinutes(5);

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(context.BridgeUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(15)
            };
            _ownsHttpClient = true;

            if (!string.IsNullOrEmpty(context.AuthToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", context.AuthToken);
            }
        }
    }

    /// <summary>Starts background polling. Safe to call only once.</summary>
    public void Start()
    {
        if (_pollingTask != null)
            throw new InvalidOperationException("Monitor already started.");

        // Set initial state from context
        var initialState = MapBridgeStatus(_context.InitialStatus);
        StateChanged?.Invoke(initialState, _context.InitialStage, null);

        _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    /// <summary>Requests cancellation of the running task via the Bridge and stops polling.</summary>
    public async Task CancelAsync()
    {
        try
        {
            _cts.Cancel();

            // Fire cancel request to Bridge (best-effort, don't await if token already fired)
            using var cancelCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await _httpClient.PostAsync(
                    $"v1/tasks/{_context.TaskId}/cancel",
                    null,
                    cancelCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: if the cancel request fails, we still stop polling
            }

            StateChanged?.Invoke(AgentTaskState.Cancelled, null, null);
        }
        catch (Exception ex)
        {
            PollingError?.Invoke($"Cancel request failed: {ex.Message}");
        }
    }

    /// <summary>Stops the monitor without sending a cancel request.</summary>
    public void Stop()
    {
        _cts.Cancel();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var consecutiveErrors = 0;
        const int maxConsecutiveErrors = 10;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check task timeout
                if (DateTime.UtcNow - startTime > _taskTimeout)
                {
                    StateChanged?.Invoke(AgentTaskState.Failed, null, Strings.ErrorTaskTimeout);
                    ErrorOccurred?.Invoke(Strings.ErrorTaskTimeout, "TASK_TIMEOUT", null, true);
                    return;
                }

                await Task.Delay(_pollingInterval, cancellationToken).ConfigureAwait(false);

                var status = await FetchTaskStatusAsync(cancellationToken).ConfigureAwait(false);

                if (status == null)
                {
                    consecutiveErrors++;
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        StateChanged?.Invoke(AgentTaskState.Disconnected, null, null);
                        return;
                    }
                    continue;
                }

                consecutiveErrors = 0;

                var state = MapBridgeStatus(status.Status);

                switch (state)
                {
                    case AgentTaskState.Completed:
                        StateChanged?.Invoke(AgentTaskState.Completed, status.Stage, null);
                        ResponseReceived?.Invoke(status.Response ?? "No response received.");
                        return;

                    case AgentTaskState.Failed:
                        StateChanged?.Invoke(AgentTaskState.Failed, status.Stage, status.Error?.Message);
                        ErrorOccurred?.Invoke(
                            status.Error?.Message ?? status.Message ?? Strings.ErrorGeneric,
                            status.Error?.Code,
                            status.Message,
                            status.Error?.Retryable ?? false);
                        return;

                    case AgentTaskState.Cancelled:
                        StateChanged?.Invoke(AgentTaskState.Cancelled, null, null);
                        return;

                    case AgentTaskState.WaitingForApproval:
                        StateChanged?.Invoke(AgentTaskState.WaitingForApproval, status.Stage, status.Message);
                        break;

                    default:
                        // Queued or Running — update progress
                        StateChanged?.Invoke(state, status.Stage, status.Message);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                consecutiveErrors++;
                PollingError?.Invoke($"Network error: {ex.Message}");

                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    StateChanged?.Invoke(AgentTaskState.Disconnected, null, null);
                    return;
                }

                // Backoff on repeated errors
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(Math.Min(consecutiveErrors * 2, 30)),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (JsonException ex)
            {
                consecutiveErrors++;
                PollingError?.Invoke($"Malformed response: {ex.Message}");

                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    StateChanged?.Invoke(AgentTaskState.Disconnected, null, null);
                    return;
                }
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (TaskCanceledException)
            {
                // HttpClient timeout
                consecutiveErrors++;
                PollingError?.Invoke("Request timed out.");

                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    StateChanged?.Invoke(AgentTaskState.Disconnected, null, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                PollingError?.Invoke($"Unexpected error: {ex.Message}");

                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    StateChanged?.Invoke(AgentTaskState.Disconnected, null, null);
                    return;
                }
            }
        }
    }

    private async Task<BridgeTaskStatus?> FetchTaskStatusAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"v1/tasks/{_context.TaskId}",
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            PollingError?.Invoke($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<BridgeTaskStatus>(json, s_jsonOptions);
    }

    private static AgentTaskState MapBridgeStatus(string? bridgeStatus)
    {
        return bridgeStatus?.ToLowerInvariant() switch
        {
            BridgeTaskStatusValues.Pending => AgentTaskState.Queued,
            BridgeTaskStatusValues.Running => AgentTaskState.Running,
            BridgeTaskStatusValues.WaitingForApproval => AgentTaskState.WaitingForApproval,
            BridgeTaskStatusValues.Completed => AgentTaskState.Completed,
            BridgeTaskStatusValues.Failed => AgentTaskState.Failed,
            BridgeTaskStatusValues.Cancelled => AgentTaskState.Cancelled,
            _ => AgentTaskState.Queued
        };
    }
}
