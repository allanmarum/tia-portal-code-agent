using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Sessions;
using TiaAgent.Bridge.Tasks;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.Bridge.Api;

public sealed class BridgeController : IDisposable
{
    private readonly HttpListener _listener;
    private readonly BridgeConfig _config;
    private readonly BridgeLogger _logger;
    private readonly TokenProvider _tokenProvider;
    private readonly OpenCodeClient _openCodeClient;
    private readonly SessionManager _sessionManager;
    private readonly TaskManager _taskManager;
    private readonly CancellationTokenSource _shutdownCts = new();

    public BridgeController(
        BridgeConfig config,
        BridgeLogger logger,
        TokenProvider tokenProvider,
        OpenCodeClient openCodeClient,
        SessionManager sessionManager,
        TaskManager taskManager)
    {
        _config = config;
        _logger = logger;
        _tokenProvider = tokenProvider;
        _openCodeClient = openCodeClient;
        _sessionManager = sessionManager;
        _taskManager = taskManager;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{config.Port}/");
    }

    public void Start()
    {
        _listener.Start();
        _ = AcceptConnectionsAsync(_shutdownCts.Token);
    }

    public void Stop()
    {
        _shutdownCts.Cancel();
        _listener.Stop();
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = HandleRequestAsync(context);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Error accepting connection", ex);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        var path = request.Url?.AbsolutePath ?? "/";
        var method = request.HttpMethod?.ToUpperInvariant() ?? "GET";

        try
        {
            // Bearer token authentication (except health check)
            if (path != "/health")
            {
                if (!AuthenticateRequest(request))
                {
                    await WriteJsonResponseAsync(response, 401, "{\"error\":\"Unauthorized\"}").ConfigureAwait(false);
                    return;
                }
            }

            switch (method, path)
            {
                case ("GET", "/health"):
                    await HandleHealthAsync(response).ConfigureAwait(false);
                    break;
                case ("POST", "/v1/tasks"):
                    await HandleCreateTaskAsync(request, response).ConfigureAwait(false);
                    break;
                case ("GET", var p) when p.StartsWith("/v1/tasks/") && p.Length > "/v1/tasks/".Length:
                    var taskId = path.Substring("/v1/tasks/".Length);
                    await HandleGetTaskStatusAsync(taskId, response).ConfigureAwait(false);
                    break;
                case ("POST", var p) when p.EndsWith("/cancel"):
                    var cancelTaskId = path.Substring("/v1/tasks/".Length);
                    cancelTaskId = cancelTaskId.Substring(0, cancelTaskId.Length - "/cancel".Length);
                    await HandleCancelTaskAsync(cancelTaskId, response).ConfigureAwait(false);
                    break;
                case ("GET", "/diagnostics"):
                    await HandleDiagnosticsAsync(response).ConfigureAwait(false);
                    break;
                default:
                    await WriteJsonResponseAsync(response, 404, "{\"error\":\"Not found\"}").ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling {method} {path}", ex);
            try
            {
                await WriteJsonResponseAsync(response, 500, "{\"error\":\"Internal server error\"}").ConfigureAwait(false);
            }
            catch { }
        }
        finally
        {
            response.Close();
        }
    }

    private bool AuthenticateRequest(HttpListenerRequest request)
    {
        var authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var token = authHeader.Substring("Bearer ".Length);
        return _tokenProvider.Validate(token);
    }

    private async Task HandleHealthAsync(HttpListenerResponse response)
    {
        var health = await _openCodeClient.HealthCheckAsync().ConfigureAwait(false);
        var result = new BridgeHealthResponse
        {
            Status = "ok",
            BridgeVersion = "1.0.0",
            OpenCodeAvailable = health.Available,
            OpenCodeVersion = health.Available ? "connected" : "unavailable",
            McpConfigured = health.Available
        };
        await WriteJsonResponseAsync(response, 200, SerializeHealthResponse(result)).ConfigureAwait(false);
    }

    private async Task HandleCreateTaskAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var body = await ReadRequestBodyAsync(request).ConfigureAwait(false);
        if (string.IsNullOrEmpty(body))
        {
            await WriteJsonResponseAsync(response, 400, "{\"error\":\"Empty request body\"}").ConfigureAwait(false);
            return;
        }

        var taskRequest = DeserializeTaskRequest(body);
        if (taskRequest == null)
        {
            await WriteJsonResponseAsync(response, 400, "{\"error\":\"Invalid request format\"}").ConfigureAwait(false);
            return;
        }

        if (_taskManager.ActiveTaskCount >= _config.MaxConcurrentTasks)
        {
            await WriteJsonResponseAsync(response, 429, "{\"error\":\"Too many concurrent tasks\",\"retryable\":true}").ConfigureAwait(false);
            return;
        }

        var taskId = _taskManager.CreateTaskAsync(taskRequest);
        var accepted = new BridgeTaskAccepted
        {
            TaskId = taskId,
            Status = BridgeTaskStatusValues.Pending,
            CorrelationId = taskRequest.CorrelationId
        };
        await WriteJsonResponseAsync(response, 202, SerializeTaskAccepted(accepted)).ConfigureAwait(false);
    }

    private async Task HandleGetTaskStatusAsync(string taskId, HttpListenerResponse response)
    {
        var status = _taskManager.GetTaskStatus(taskId);
        if (status == null)
        {
            await WriteJsonResponseAsync(response, 404, "{\"error\":\"Task not found\"}").ConfigureAwait(false);
            return;
        }

        var result = new BridgeTaskStatus
        {
            TaskId = status.TaskId,
            Status = status.Status,
            Stage = status.Stage ?? "",
            Message = status.Message ?? "",
            Response = status.Response ?? "",
            Error = status.Error
        };
        await WriteJsonResponseAsync(response, 200, SerializeTaskStatus(result)).ConfigureAwait(false);
    }

    private async Task HandleCancelTaskAsync(string taskId, HttpListenerResponse response)
    {
        var cancelled = _taskManager.CancelTask(taskId);
        if (!cancelled)
        {
            await WriteJsonResponseAsync(response, 404, "{\"error\":\"Task not found or already completed\"}").ConfigureAwait(false);
            return;
        }

        await WriteJsonResponseAsync(response, 200, "{\"status\":\"cancelled\"}").ConfigureAwait(false);
    }

    private async Task HandleDiagnosticsAsync(HttpListenerResponse response)
    {
        var diagnostics = new
        {
            bridge = new
            {
                version = "1.0.0",
                port = _config.Port,
                maxConcurrentTasks = _config.MaxConcurrentTasks
            },
            sessions = new
            {
                activeCount = _sessionManager.ActiveSessionCount
            },
            tasks = new
            {
                activeCount = _taskManager.ActiveTaskCount
            }
        };
        await WriteJsonResponseAsync(response, 200, SerializeDiagnostics(diagnostics)).ConfigureAwait(false);
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, int statusCode, string json)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 <= 0) return null;
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string SerializeHealthResponse(BridgeHealthResponse r) =>
        $"{{\"status\":\"{r.Status}\",\"bridgeVersion\":\"{r.BridgeVersion}\",\"openCodeAvailable\":{r.OpenCodeAvailable.ToString().ToLowerInvariant()},\"openCodeVersion\":\"{r.OpenCodeVersion}\",\"mcpConfigured\":{r.McpConfigured.ToString().ToLowerInvariant()}}}";

    private static string SerializeTaskAccepted(BridgeTaskAccepted a) =>
        $"{{\"taskId\":\"{a.TaskId}\",\"status\":\"{a.Status}\",\"correlationId\":\"{a.CorrelationId}\"}}";

    private static string SerializeTaskStatus(BridgeTaskStatus s)
    {
        var errorJson = s.Error != null
            ? $",\"error\":{{\"code\":\"{s.Error.Code}\",\"message\":\"{EscapeJson(s.Error.Message)}\",\"retryable\":{s.Error.Retryable.ToString().ToLowerInvariant()}}}"
            : "";
        return $"{{\"taskId\":\"{s.TaskId}\",\"status\":\"{s.Status}\",\"stage\":\"{EscapeJson(s.Stage)}\",\"message\":\"{EscapeJson(s.Message)}\",\"response\":\"{EscapeJson(s.Response)}\"{errorJson}}}";
    }

    private static string SerializeDiagnostics(object d) =>
        System.Text.Json.JsonSerializer.Serialize(d);

    private static string EscapeJson(string? s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    private static BridgeTaskRequest? DeserializeTaskRequest(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<BridgeTaskRequest>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _listener.Stop();
        _listener.Close();
    }
}
