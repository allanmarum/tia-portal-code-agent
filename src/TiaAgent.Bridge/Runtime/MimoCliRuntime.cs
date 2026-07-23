using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Runtime adapter for Mimo CLI (mimo run --format json).
/// Executes tasks via non-interactive CLI mode. Does not require the Mimo Web UI.
/// </summary>
public sealed class MimoCliRuntime : IAgentRuntime, IDisposable
{
    private readonly ProcessRunner _processRunner;
    private readonly BridgeLogger _logger;
    private readonly string? _executable;
    private readonly string? _model;
    private static readonly char[] s_lineSeparators = new[] { '\n', '\r' };

    public string Id => "mimo";
    public string DisplayName => "Mimo CLI";

    public MimoCliRuntime(BridgeLogger logger, string? executable = null, string? model = null)
    {
        _logger = logger;
        _processRunner = new ProcessRunner(logger);
        _executable = executable;
        _model = model;
    }

    public async Task<RuntimeAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var exe = _executable ?? "mimo";
        try
        {
            var result = await _processRunner.RunAsync(
                exe, "--version", null,
                TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                var version = result.StdOut.Trim();
                _logger.Info($"MimoCliRuntime: available, version={version}");
                return new RuntimeAvailabilityResult
                {
                    Available = true,
                    Executable = exe,
                    Version = version,
                    Mode = "cli"
                };
            }

            var error = result.Error ?? $"mimo returned exit code {result.ExitCode}: {result.StdErr.Trim()}";
            _logger.Warn($"MimoCliRuntime: not available, error={error}");
            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = exe,
                Mode = "cli",
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.Warn($"MimoCliRuntime: executable not found: {ex.Message}");
            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = exe,
                Error = $"Executable not found: {exe}. {ex.Message}"
            };
        }
    }

    public async Task<AgentTaskResult> ExecuteAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken)
    {
        var exe = _executable ?? "mimo";

        // Build arguments: mimo run "<prompt>" --format json --agent <agentId> --never-ask --trust
        var args = BuildArguments(request);

        _logger.Info($"MimoCliRuntime: executing task {request.TaskId} (action={request.Action}, agent={request.AgentId})");

        var lineProgress = new Progress<string>(line =>
        {
            progress?.Report(new AgentTaskEvent
            {
                EventType = "progress",
                Message = line
            });
        });

        var result = await _processRunner.RunAsync(
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
                RuntimeId = Id
            };
        }

        if (result.TimedOut)
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = result.Error ?? "Task timed out",
                ErrorCode = "TASK_TIMEOUT",
                RuntimeId = Id
            };
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = result.Error,
                ErrorCode = "RUNTIME_EXECUTION_FAILED",
                RuntimeId = Id
            };
        }

        // Parse the JSON output from mimo run --format json
        // mimo outputs JSONL events, the final response is in the last event
        var response = ParseMimoJsonOutput(result.StdOut, result.StdErr);

        if (result.ExitCode != 0 && string.IsNullOrEmpty(response))
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = $"mimo exited with code {result.ExitCode}. {ProcessRunner.StripAnsiEscapes(result.StdErr.Trim())}",
                ErrorCode = "RUNTIME_NON_ZERO_EXIT",
                RuntimeId = Id
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

    public Task CancelAsync(string taskId, CancellationToken cancellationToken)
    {
        // Cancellation is handled via CancellationToken passed to ExecuteAsync
        _logger.Info($"MimoCliRuntime: cancel requested for task {taskId} (handled via CTS)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the command-line arguments for mimo run.
    /// </summary>
    private string BuildArguments(AgentTaskRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("run");

        // Prompt — use --prompt flag for safe passing (no shell interpretation)
        if (!string.IsNullOrEmpty(request.Prompt))
        {
            sb.Append(" --prompt ");
            sb.Append(EscapeShellArg(request.Prompt));
        }

        // JSON output format for machine-readable events
        sb.Append(" --format json");

        // Agent profile
        if (!string.IsNullOrEmpty(request.AgentId))
        {
            sb.Append(" --agent ");
            sb.Append(EscapeShellArg(request.AgentId));
        }

        // Model override
        if (!string.IsNullOrEmpty(_model))
        {
            sb.Append(" --model ");
            sb.Append(_model);
        }

        // Non-interactive flags
        sb.Append(" --never-ask");
        sb.Append(" --trust");

        return sb.ToString();
    }

    /// <summary>
    /// Parses mimo's JSONL output to extract the final response.
    /// mimo --format json outputs one JSON object per line.
    /// The response is typically in the last event with type "assistant" or "result".
    /// </summary>
    private string ParseMimoJsonOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            // Fallback: use stderr as response if stdout is empty
            return ProcessRunner.StripAnsiEscapes(stderr.Trim());
        }

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

                // Look for content in various event types
                if (root.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();

                    // mimo JSON events: "assistant" type contains the response
                    if (type == "assistant" || type == "result" || type == "message")
                    {
                        if (root.TryGetProperty("content", out var contentProp))
                        {
                            lastContent = contentProp.GetString();
                        }
                        else if (root.TryGetProperty("text", out var textProp))
                        {
                            lastContent = textProp.GetString();
                        }
                        else if (root.TryGetProperty("message", out var msgProp))
                        {
                            lastContent = msgProp.GetString();
                        }
                    }
                }

                // Also check for "result" at top level
                if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.String)
                {
                    lastContent = resultProp.GetString();
                }
            }
            catch (JsonException)
            {
                // Not valid JSON, skip
            }
        }

        // If we couldn't parse structured events, use the full stdout
        if (lastContent == null)
        {
            lastContent = ProcessRunner.StripAnsiEscapes(stdout.Trim());
        }

        return lastContent ?? "";
    }

    private static string EscapeShellArg(string arg) => RuntimeHelpers.EscapeShellArg(arg);

    public void Dispose()
    {
        _processRunner.Dispose();
    }
}
