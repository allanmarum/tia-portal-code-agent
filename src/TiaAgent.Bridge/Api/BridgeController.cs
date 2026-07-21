using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Sessions;
using TiaAgent.Bridge.Tasks;
using TiaAgent.Contracts.Bridge;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Api;

public sealed class BridgeController : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions s_jsonWriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpListener _listener;
    private readonly BridgeConfig _config;
    private readonly BridgeLogger _logger;
    private readonly TokenProvider _tokenProvider;
    private readonly RuntimeRegistry _runtimeRegistry;
    private readonly TaskManager _taskManager;
    private readonly CancellationTokenSource _shutdownCts = new();

    public BridgeController(
        BridgeConfig config,
        BridgeLogger logger,
        TokenProvider tokenProvider,
        RuntimeRegistry runtimeRegistry,
        TaskManager taskManager)
    {
        _config = config;
        _logger = logger;
        _tokenProvider = tokenProvider;
        _runtimeRegistry = runtimeRegistry;
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
                var (authenticated, errorType, errorMessage) = AuthenticateRequest(request);
                if (!authenticated)
                {
                    _logger.Warn($"Auth failed: type={errorType}, path={path}, method={method}");
                    var errorJson = $"{{\"error\":\"Unauthorized\",\"errorType\":\"{errorType}\",\"message\":\"{EscapeJson(errorMessage)}\"}}";
                    await WriteJsonResponseAsync(response, 401, errorJson).ConfigureAwait(false);
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

                // New runtime endpoints
                case ("GET", "/api/runtimes"):
                    await HandleListRuntimesAsync(response).ConfigureAwait(false);
                    break;
                case ("GET", var p) when p.StartsWith("/api/runtimes/") && p.EndsWith("/health"):
                    var runtimeId = ExtractSegment(path, "/api/runtimes/", "/health");
                    if (runtimeId != null)
                        await HandleRuntimeHealthAsync(runtimeId, response).ConfigureAwait(false);
                    else
                        await WriteJsonResponseAsync(response, 404, "{\"error\":\"Not found\"}").ConfigureAwait(false);
                    break;
                case ("GET", "/api/settings/runtime"):
                    await HandleGetRuntimeSettingsAsync(response).ConfigureAwait(false);
                    break;
                case ("PUT", "/api/settings/runtime"):
                    await HandlePutRuntimeSettingsAsync(request, response).ConfigureAwait(false);
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

    private (bool success, string errorType, string message) AuthenticateRequest(HttpListenerRequest request)
    {
        var authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader))
            return (false, "missing", "Authorization header is required");

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (false, "malformed", "Authorization header must use 'Bearer' scheme");

        var token = authHeader.Substring("Bearer ".Length);
        if (string.IsNullOrWhiteSpace(token))
            return (false, "malformed", "Bearer token is empty");

        if (!_tokenProvider.Validate(token))
            return (false, "invalid", "Invalid authentication token");

        return (true, "", "");
    }

    private async Task HandleHealthAsync(HttpListenerResponse response)
    {
        var instanceId = Environment.GetEnvironmentVariable("TIA_AGENT_INSTANCE_ID") ?? "";

        // Check the configured default runtime availability
        var defaultRuntimeId = _runtimeRegistry.GetDefaultRuntimeId();
        IAgentRuntime? defaultRuntime = null;
        RuntimeAvailabilityResult? availability = null;
        try
        {
            defaultRuntime = _runtimeRegistry.GetRuntime(defaultRuntimeId);
            availability = await defaultRuntime.CheckAvailabilityAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch { }

        var healthJson = $"{{\"service\":\"tia-agent-bridge\",\"status\":\"healthy\",\"version\":\"1.0.0\",\"instanceId\":\"{EscapeJson(instanceId)}\",\"runtimeId\":\"{EscapeJson(defaultRuntimeId)}\",\"runtimeDisplayName\":\"{EscapeJson(defaultRuntime?.DisplayName ?? defaultRuntimeId)}\",\"runtimeAvailable\":{(availability?.Available == true ? "true" : "false")},\"runtimeVersion\":\"{EscapeJson(availability?.Version ?? "")}\"}}";
        await WriteJsonResponseAsync(response, 200, healthJson).ConfigureAwait(false);
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
            Error = status.Error,
            RuntimeId = status.RuntimeId,
            RuntimeVersion = status.RuntimeVersion
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

    #region Runtime Endpoints

    private async Task HandleListRuntimesAsync(HttpListenerResponse response)
    {
        var runtimes = _runtimeRegistry.GetAllRuntimes();
        var availability = await _runtimeRegistry.CheckAllAvailabilityAsync(CancellationToken.None).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.Append("{\"runtimes\":[");
        var first = true;
        foreach (var runtime in runtimes.OrderBy(r => r.Id))
        {
            if (!first) sb.Append(',');
            first = false;

            var avail = availability.GetValueOrDefault(runtime.Id);
            sb.Append('{');
            sb.Append($"\"id\":\"{EscapeJson(runtime.Id)}\"");
            sb.Append($",\"displayName\":\"{EscapeJson(runtime.DisplayName)}\"");
            sb.Append($",\"available\":{(avail?.Available == true ? "true" : "false")}");
            sb.Append($",\"version\":\"{EscapeJson(avail?.Version ?? "")}\"");
            sb.Append($",\"mode\":\"{EscapeJson(avail?.Mode ?? "")}\"");
            if (!string.IsNullOrEmpty(avail?.Error))
                sb.Append($",\"error\":\"{EscapeJson(avail.Error)}\"");
            sb.Append('}');
        }
        sb.Append("],\"default\":\"");
        sb.Append(EscapeJson(_runtimeRegistry.GetDefaultRuntimeId()));
        sb.Append("\"}");

        await WriteJsonResponseAsync(response, 200, sb.ToString()).ConfigureAwait(false);
    }

    private async Task HandleRuntimeHealthAsync(string runtimeId, HttpListenerResponse response)
    {
        IAgentRuntime runtime;
        try
        {
            runtime = _runtimeRegistry.GetRuntime(runtimeId);
        }
        catch (InvalidOperationException ex)
        {
            await WriteJsonResponseAsync(response, 404, $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}").ConfigureAwait(false);
            return;
        }

        var availability = await runtime.CheckAvailabilityAsync(CancellationToken.None).ConfigureAwait(false);
        var json = $"{{\"id\":\"{EscapeJson(runtime.Id)}\",\"displayName\":\"{EscapeJson(runtime.DisplayName)}\",\"available\":{(availability.Available ? "true" : "false")},\"version\":\"{EscapeJson(availability.Version ?? "")}\",\"mode\":\"{EscapeJson(availability.Mode ?? "")}\",\"executable\":\"{EscapeJson(availability.Executable ?? "")}\"}}";
        await WriteJsonResponseAsync(response, 200, json).ConfigureAwait(false);
    }

    private async Task HandleGetRuntimeSettingsAsync(HttpListenerResponse response)
    {
        var defaultId = _runtimeRegistry.GetDefaultRuntimeId();
        var json = $"{{\"defaultRuntime\":\"{EscapeJson(defaultId)}\"}}";
        await WriteJsonResponseAsync(response, 200, json).ConfigureAwait(false);
    }

    private async Task HandlePutRuntimeSettingsAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var body = await ReadRequestBodyAsync(request).ConfigureAwait(false);
        if (string.IsNullOrEmpty(body))
        {
            await WriteJsonResponseAsync(response, 400, "{\"error\":\"Empty request body\"}").ConfigureAwait(false);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("defaultRuntime", out var defaultRuntimeProp))
            {
                await WriteJsonResponseAsync(response, 400, "{\"error\":\"Missing 'defaultRuntime' field\"}").ConfigureAwait(false);
                return;
            }

            var newDefault = defaultRuntimeProp.GetString();
            if (string.IsNullOrEmpty(newDefault))
            {
                await WriteJsonResponseAsync(response, 400, "{\"error\":\"'defaultRuntime' cannot be empty\"}").ConfigureAwait(false);
                return;
            }

            // Validate the runtime exists
            try
            {
                _runtimeRegistry.GetRuntime(newDefault);
            }
            catch (InvalidOperationException ex)
            {
                await WriteJsonResponseAsync(response, 400, $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}").ConfigureAwait(false);
                return;
            }

            // Update the config file
            var configPath = RuntimeConfigLoader.GetConfigPath();
            var configDir = Path.GetDirectoryName(configPath);
            if (configDir != null && !Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            TiaAgentConfig config;
            if (File.Exists(configPath))
            {
                var configJson = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<TiaAgentConfig>(configJson, s_jsonOptions) ?? new TiaAgentConfig();
            }
            else
            {
                config = new TiaAgentConfig();
            }

            // Update the default runtime
            var updatedConfig = new TiaAgentConfig
            {
                DefaultRuntime = newDefault,
                Runtimes = config.Runtimes
            };

            var outputJson = JsonSerializer.Serialize(updatedConfig, s_jsonWriteOptions);
            File.WriteAllText(configPath, outputJson);

            _logger.Info($"Default runtime changed to '{newDefault}'");
            await WriteJsonResponseAsync(response, 200, $"{{\"defaultRuntime\":\"{EscapeJson(newDefault)}\"}}").ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await WriteJsonResponseAsync(response, 400, "{\"error\":\"Invalid JSON\"}").ConfigureAwait(false);
        }
    }

    #endregion

    private async Task HandleDiagnosticsAsync(HttpListenerResponse response)
    {
        var defaultRuntimeId = _runtimeRegistry.GetDefaultRuntimeId();
        var diagnostics = new
        {
            bridge = new
            {
                version = "1.0.0",
                port = _config.Port,
                pid = Environment.ProcessId,
                maxConcurrentTasks = _config.MaxConcurrentTasks,
                authTokenFingerprint = TokenFingerprint(_tokenProvider.Token)
            },
            runtime = new
            {
                defaultId = defaultRuntimeId,
                registeredCount = _runtimeRegistry.GetAllRuntimes().Count
            },
            tasks = new
            {
                activeCount = _taskManager.ActiveTaskCount
            }
        };
        await WriteJsonResponseAsync(response, 200, SerializeDiagnostics(diagnostics)).ConfigureAwait(false);
    }

    private static string TokenFingerprint(string token)
    {
        if (string.IsNullOrEmpty(token)) return "<empty>";
        return token.Length > 8
            ? $"{token[..4]}...{token[^4..]} ({token.Length} chars)"
            : $"{token[..2]}... ({token.Length} chars)";
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

    private static string SerializeTaskAccepted(BridgeTaskAccepted a) =>
        $"{{\"taskId\":\"{a.TaskId}\",\"status\":\"{a.Status}\",\"correlationId\":\"{a.CorrelationId}\"}}";

    private static string SerializeTaskStatus(BridgeTaskStatus s)
    {
        var errorJson = s.Error != null
            ? $",\"error\":{{\"code\":\"{s.Error.Code}\",\"message\":\"{EscapeJson(s.Error.Message)}\",\"retryable\":{s.Error.Retryable.ToString().ToLowerInvariant()}}}"
            : "";
        var runtimeJson = !string.IsNullOrEmpty(s.RuntimeId)
            ? $",\"runtimeId\":\"{EscapeJson(s.RuntimeId)}\",\"runtimeVersion\":\"{EscapeJson(s.RuntimeVersion ?? "")}\""
            : "";
        return $"{{\"taskId\":\"{s.TaskId}\",\"status\":\"{s.Status}\",\"stage\":\"{EscapeJson(s.Stage)}\",\"message\":\"{EscapeJson(s.Message)}\",\"response\":\"{EscapeJson(s.Response)}\"{errorJson}{runtimeJson}}}";
    }

    private static string SerializeDiagnostics(object d) =>
        System.Text.Json.JsonSerializer.Serialize(d);

    private static string EscapeJson(string? s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    private BridgeTaskRequest? DeserializeTaskRequest(string json)
    {
        try
        {
            var request = System.Text.Json.JsonSerializer.Deserialize<BridgeTaskRequest>(json, s_jsonOptions);
            if (request != null)
            {
                _logger.Info($"Deserialized request: action='{request.Action}', agentId='{request.AgentId}', runtime='{request.Runtime ?? "default"}', project={request.Project != null}, selection={request.Selection != null}");
            }
            return request;
        }
        catch (Exception ex)
        {
            _logger.Error($"JSON deserialization failed: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Extracts a segment between two path prefixes. E.g. "/api/runtimes/claude/health" with "/api/runtimes/" and "/health" returns "claude".
    /// </summary>
    private static string? ExtractSegment(string path, string prefix, string suffix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var start = prefix.Length;
        var end = path.Length - suffix.Length;
        if (end <= start) return null;

        return path.Substring(start, end - start);
    }

    public void Dispose()
    {
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _listener.Stop();
        _listener.Close();
    }
}
