using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Runtime adapter for OpenCode. Supports two modes:
/// - Server mode: HTTP client to a running OpenCode server (default)
/// - CLI mode: opencode run --format json (non-interactive)
///
/// The mode is determined by configuration. No silent fallback between modes.
/// </summary>
public sealed class OpenCodeRuntime : IAgentRuntime, IDisposable
{
    private readonly BridgeLogger _logger;
    private readonly OpenCodeClient? _serverClient;
    private readonly ProcessRunner? _processRunner;
    private readonly string _mode;
    private readonly string? _executable;
    private readonly string? _model;
    private readonly string _serverUrl;
    private static readonly char[] s_lineSeparators = new[] { '\n', '\r' };

    public string Id => "opencode";
    public string DisplayName => "OpenCode";

    /// <summary>
    /// Creates an OpenCode runtime in the specified mode.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="mode">"server" or "cli".</param>
    /// <param name="serverUrl">Server URL for server mode (e.g. "http://127.0.0.1:43120").</param>
    /// <param name="executable">Executable path/name for CLI mode.</param>
    /// <param name="model">Optional model override.</param>
    public OpenCodeRuntime(
        BridgeLogger logger,
        string mode = "server",
        string serverUrl = "http://127.0.0.1:43120",
        string? executable = null,
        string? model = null)
    {
        _logger = logger;
        _mode = mode;
        _serverUrl = serverUrl;
        _executable = executable;
        _model = model;

        if (_mode == "server")
        {
            _serverClient = new OpenCodeClient(serverUrl, TimeSpan.FromMinutes(5));
        }
        else
        {
            _processRunner = new ProcessRunner(logger);
        }
    }

    public async Task<RuntimeAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        if (_mode == "server")
            return await CheckServerAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        else
            return await CheckCliAvailabilityAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentTaskResult> ExecuteAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken)
    {
        if (_mode == "server")
            return await ExecuteViaServerAsync(request, progress, cancellationToken).ConfigureAwait(false);
        else
            return await ExecuteViaCliAsync(request, progress, cancellationToken).ConfigureAwait(false);
    }

    public Task CancelAsync(string taskId, CancellationToken cancellationToken)
    {
        _logger.Info($"OpenCodeRuntime: cancel requested for task {taskId}");
        // Cancellation is handled via CancellationToken in ExecuteAsync
        return Task.CompletedTask;
    }

    #region Server Mode

    private async Task<RuntimeAvailabilityResult> CheckServerAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await _serverClient!.HealthCheckAsync(cancellationToken).ConfigureAwait(false);
            return new RuntimeAvailabilityResult
            {
                Available = health.Available,
                Executable = _serverUrl,
                Version = health.Available ? "connected" : null,
                Mode = "server",
                Error = health.Available ? null : $"OpenCode server at {_serverUrl} is not responding"
            };
        }
        catch (Exception ex)
        {
            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = _serverUrl,
                Mode = "server",
                Error = $"OpenCode server check failed: {ex.Message}"
            };
        }
    }

    private async Task<AgentTaskResult> ExecuteViaServerAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken)
    {
        _logger.Info($"OpenCodeRuntime(server): executing task {request.TaskId}");

        try
        {
            // Create session
            progress?.Report(new AgentTaskEvent { EventType = "progress", Message = "Creating session..." });

            var sessionResponse = await _serverClient!.CreateSessionAsync(
                request.AgentId, request.Prompt, cancellationToken).ConfigureAwait(false);

            if (!sessionResponse.Success || string.IsNullOrEmpty(sessionResponse.SessionId))
            {
                return new AgentTaskResult
                {
                    Success = false,
                    Error = $"Failed to create OpenCode session: {sessionResponse.RawJson}",
                    ErrorCode = "RUNTIME_SESSION_FAILED",
                    RuntimeId = Id,
                    RuntimeMode = "server"
                };
            }

            // Send message
            progress?.Report(new AgentTaskEvent { EventType = "progress", Message = "Processing..." });

            var messageResponse = await _serverClient.SendMessageAsync(
                sessionResponse.SessionId, request.Prompt, cancellationToken).ConfigureAwait(false);

            if (!messageResponse.Success)
            {
                return new AgentTaskResult
                {
                    Success = false,
                    Error = $"OpenCode message failed: {messageResponse.RawJson}",
                    ErrorCode = "RUNTIME_TASK_FAILED",
                    RuntimeId = Id,
                    RuntimeMode = "server"
                };
            }

            return new AgentTaskResult
            {
                Success = true,
                Response = messageResponse.RawJson,
                RuntimeId = Id,
                RuntimeMode = "server"
            };
        }
        catch (OperationCanceledException)
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = "Task was cancelled",
                ErrorCode = "TASK_CANCELLED",
                RuntimeId = Id,
                RuntimeMode = "server"
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"OpenCodeRuntime(server): task {request.TaskId} failed", ex);
            return new AgentTaskResult
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = "RUNTIME_EXECUTION_FAILED",
                RuntimeId = Id,
                RuntimeMode = "server"
            };
        }
    }

    #endregion

    #region CLI Mode

    private async Task<RuntimeAvailabilityResult> CheckCliAvailabilityAsync(CancellationToken cancellationToken)
    {
        var exe = _executable ?? "opencode";
        try
        {
            var result = await _processRunner!.RunAsync(
                exe, "--version", null,
                TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                return new RuntimeAvailabilityResult
                {
                    Available = true,
                    Executable = exe,
                    Version = result.StdOut.Trim(),
                    Mode = "cli"
                };
            }

            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = exe,
                Mode = "cli",
                Error = $"opencode returned exit code {result.ExitCode}: {result.StdErr.Trim()}"
            };
        }
        catch (Exception ex)
        {
            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = exe,
                Mode = "cli",
                Error = $"Executable not found: {exe}. {ex.Message}"
            };
        }
    }

    private async Task<AgentTaskResult> ExecuteViaCliAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken)
    {
        var exe = _executable ?? "opencode";
        var args = BuildCliArguments(request);

        _logger.Info($"OpenCodeRuntime(cli): executing task {request.TaskId}");

        var lineProgress = new Progress<string>(line =>
        {
            progress?.Report(new AgentTaskEvent { EventType = "progress", Message = line });
        });

        var result = await _processRunner!.RunAsync(
            exe, args, null,
            TimeSpan.FromMinutes(5),
            progress: lineProgress,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Cancelled)
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = "Task was cancelled",
                ErrorCode = "TASK_CANCELLED",
                RuntimeId = Id,
                RuntimeMode = "cli"
            };
        }

        if (result.TimedOut)
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = result.Error ?? "Task timed out",
                ErrorCode = "TASK_TIMEOUT",
                RuntimeId = Id,
                RuntimeMode = "cli"
            };
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = result.Error,
                ErrorCode = "RUNTIME_EXECUTION_FAILED",
                RuntimeId = Id,
                RuntimeMode = "cli"
            };
        }

        var response = ParseOpenCodeOutput(result.StdOut, result.StdErr);

        if (result.ExitCode != 0 && string.IsNullOrEmpty(response))
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = $"opencode exited with code {result.ExitCode}. {ProcessRunner.StripAnsiEscapes(result.StdErr.Trim())}",
                ErrorCode = "RUNTIME_NON_ZERO_EXIT",
                RuntimeId = Id,
                RuntimeMode = "cli"
            };
        }

        return new AgentTaskResult
        {
            Success = true,
            Response = response,
            RuntimeId = Id,
            RuntimeMode = "cli"
        };
    }

    private string BuildCliArguments(AgentTaskRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("run");

        if (!string.IsNullOrEmpty(request.Prompt))
        {
            sb.Append(" --prompt ");
            sb.Append(EscapeShellArg(request.Prompt));
        }

        sb.Append(" --format json");

        if (!string.IsNullOrEmpty(request.AgentId))
        {
            sb.Append(" --agent ");
            sb.Append(EscapeShellArg(request.AgentId));
        }

        if (!string.IsNullOrEmpty(_model))
        {
            sb.Append(" --model ");
            sb.Append(_model);
        }

        return sb.ToString();
    }

    private string ParseOpenCodeOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return ProcessRunner.StripAnsiEscapes(stderr.Trim());

        var lines = stdout.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
        string? lastContent = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (type == "assistant" || type == "result" || type == "message")
                    {
                        if (root.TryGetProperty("content", out var contentProp))
                            lastContent = contentProp.GetString();
                        else if (root.TryGetProperty("text", out var textProp))
                            lastContent = textProp.GetString();
                    }
                }

                if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.String)
                    lastContent = resultProp.GetString();
            }
            catch (JsonException) { }
        }

        return lastContent ?? ProcessRunner.StripAnsiEscapes(stdout.Trim());
    }

    private static string EscapeShellArg(string arg) => RuntimeHelpers.EscapeShellArg(arg);

    #endregion

    public void Dispose()
    {
        _serverClient?.Dispose();
        _processRunner?.Dispose();
    }
}
