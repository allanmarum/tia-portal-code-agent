using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Runtime adapter for Claude Code CLI (claude -p --output-format json).
/// Uses non-interactive print mode with MCP configuration for TIA Portal tools.
/// Does not automate the terminal UI or send simulated keyboard input.
/// </summary>
public sealed class ClaudeCodeRuntime : IAgentRuntime, IDisposable
{
    private readonly ProcessRunner _processRunner;
    private readonly BridgeLogger _logger;
    private readonly string? _executable;
    private readonly string? _model;
    private readonly string? _mcpServerCommand;
    private string? _generatedMcpConfigPath;

    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public string Id => "claude";
    public string DisplayName => "Claude Code CLI";

    /// <summary>
    /// Creates a Claude Code runtime adapter.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="executable">Path to claude executable.</param>
    /// <param name="model">Optional model override.</param>
    /// <param name="mcpServerCommand">The MCP server command (e.g. "tia-mcp"). If set, generates an MCP config file.</param>
    public ClaudeCodeRuntime(BridgeLogger logger, string? executable = null, string? model = null, string? mcpServerCommand = null)
    {
        _logger = logger;
        _processRunner = new ProcessRunner(logger);
        _executable = executable;
        _model = model;
        _mcpServerCommand = mcpServerCommand;
    }

    public async Task<RuntimeAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var exeName = _executable ?? "claude";
        var (fileName, psPrefix) = ResolveProcess(exeName);
        try
        {
            var result = await _processRunner.RunAsync(
                fileName, $"{psPrefix}--version".TrimStart(), null,
                TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                var version = result.StdOut.Trim();
                _logger.Info($"ClaudeCodeRuntime: available, version={version}");

                // Generate MCP config if we have an MCP server command
                if (!string.IsNullOrEmpty(_mcpServerCommand))
                {
                    EnsureMcpConfigGenerated();
                }

                return new RuntimeAvailabilityResult
                {
                    Available = true,
                    Executable = exeName,
                    Version = version,
                    Mode = "cli"
                };
            }

            var error = result.Error ?? $"claude returned exit code {result.ExitCode}: {result.StdErr.Trim()}";
            _logger.Warn($"ClaudeCodeRuntime: not available, error={error}");
            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = exeName,
                Mode = "cli",
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.Warn($"ClaudeCodeRuntime: executable not found: {ex.Message}");
            return new RuntimeAvailabilityResult
            {
                Available = false,
                Executable = exeName,
                Mode = "cli",
                Error = $"Executable not found: {exeName}. {ex.Message}"
            };
        }
    }

    public async Task<AgentTaskResult> ExecuteAsync(
        AgentTaskRequest request,
        IProgress<AgentTaskEvent>? progress,
        CancellationToken cancellationToken)
    {
        var exeName = _executable ?? "claude";
        var (fileName, psPrefix) = ResolveProcess(exeName);

        // Ensure MCP config exists before building arguments (--mcp-config needs the file)
        if (!string.IsNullOrEmpty(_mcpServerCommand))
        {
            EnsureMcpConfigGenerated();
        }

        var claudeArgs = BuildArguments(request);
        _logger.Info($"ClaudeCodeRuntime: mcpConfig={_generatedMcpConfigPath ?? "none"}, psWrap={psPrefix.Length > 0}");
        var args = $"{psPrefix}{claudeArgs}".TrimStart();

        _logger.Info($"ClaudeCodeRuntime: executing task {request.TaskId} (action={request.Action}, agent={request.AgentId}, mcpConfig={_generatedMcpConfigPath ?? "none"})");

        var lineProgress = new Progress<string>(line =>
        {
            progress?.Report(new AgentTaskEvent { EventType = "progress", Message = line });
        });

        var result = await _processRunner.RunAsync(
            fileName, args, null,
            TimeSpan.FromMinutes(5),
            progress: lineProgress,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Cancelled)
        {
            return new AgentTaskResult
            {
                Success = false, Error = "Task was cancelled",
                ErrorCode = "TASK_CANCELLED", RuntimeId = Id
            };
        }

        if (result.TimedOut)
        {
            return new AgentTaskResult
            {
                Success = false, Error = result.Error ?? "Task timed out",
                ErrorCode = "TASK_TIMEOUT", RuntimeId = Id
            };
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            return new AgentTaskResult
            {
                Success = false, Error = result.Error,
                ErrorCode = "RUNTIME_EXECUTION_FAILED", RuntimeId = Id
            };
        }

        // Parse Claude's JSON output
        var response = ParseClaudeOutput(result.StdOut, result.StdErr);

        if (result.ExitCode != 0 && string.IsNullOrEmpty(response))
        {
            return new AgentTaskResult
            {
                Success = false,
                Error = $"claude exited with code {result.ExitCode}. {ProcessRunner.StripAnsiEscapes(result.StdErr.Trim())}",
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
        _logger.Info($"ClaudeCodeRuntime: cancel requested for task {taskId}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the executable for process start. If the executable is a .ps1 script
    /// (or a bare name whose .ps1 variant exists on PATH), returns powershell.exe as
    /// the FileName and a prefix that invokes the original script via -File.
    /// </summary>
    private static (string FileName, string ArgPrefix) ResolveProcess(string executable)
    {
        string? scriptPath = null;

        if (executable.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            scriptPath = executable;
        }
        else if (!executable.Contains('.') &&
                 !executable.Contains(Path.DirectorySeparatorChar) &&
                 !executable.Contains(Path.AltDirectorySeparatorChar))
        {
            // Bare name like "claude" — check if claude.ps1 exists on PATH
            var ps1 = FindOnPath(executable + ".ps1");
            if (ps1 != null)
            {
                scriptPath = ps1;
            }
        }

        if (scriptPath != null)
        {
            // Wrap: powershell.exe -NoProfile -ExecutionPolicy Bypass -File "script.ps1" <args>
            var prefix = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" ";
            return ("powershell.exe", prefix);
        }

        return (executable, "");
    }

    /// <summary>
    /// Returns the full path if the file is found on PATH, null otherwise.
    /// </summary>
    private static string? FindOnPath(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    /// <summary>
    /// Builds the command-line arguments for claude -p.
    /// </summary>
    private string BuildArguments(AgentTaskRequest request)
    {
        var sb = new StringBuilder();

        // Non-interactive print mode
        sb.Append("-p");

        // Prompt — passed as a positional argument
        if (!string.IsNullOrEmpty(request.Prompt))
        {
            sb.Append(' ');
            sb.Append(EscapeShellArg(request.Prompt));
        }

        // JSON output for machine-readable response
        sb.Append(" --output-format json");

        // MCP configuration for TIA Portal tools
        if (!string.IsNullOrEmpty(_generatedMcpConfigPath))
        {
            sb.Append(" --mcp-config ");
            sb.Append(_generatedMcpConfigPath);
            sb.Append(" --strict-mcp-config");

        }

        // Model override
        if (!string.IsNullOrEmpty(_model))
        {
            sb.Append(" --model ");
            sb.Append(_model);
        }

        // Skip permissions for non-interactive use
        sb.Append(" --dangerously-skip-permissions");

        // No session persistence for one-shot tasks
        sb.Append(" --no-session-persistence");

        return sb.ToString();
    }

    /// <summary>
    /// Parses Claude's JSON output (--output-format json returns a single JSON object).
    /// The response text is in the "result" field.
    /// </summary>
    private string ParseClaudeOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return ProcessRunner.StripAnsiEscapes(stderr.Trim());

        try
        {
            using var doc = JsonDocument.Parse(stdout.Trim());
            var root = doc.RootElement;

            // Claude --output-format json: { "result": "...", "is_error": false, ... }
            if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.String)
            {
                return resultProp.GetString() ?? "";
            }

            // Alternative: check for "content" or "text"
            if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                return contentProp.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // Not valid JSON — treat as plain text
        }

        return ProcessRunner.StripAnsiEscapes(stdout.Trim());
    }

    /// <summary>
    /// Generates an MCP configuration file for Claude Code pointing to the tia-mcp stdio server.
    /// </summary>
    private void EnsureMcpConfigGenerated()
    {
        if (_generatedMcpConfigPath != null && File.Exists(_generatedMcpConfigPath))
            return;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configDir = Path.Combine(localAppData, "TiaAgent");
            Directory.CreateDirectory(configDir);

            _generatedMcpConfigPath = Path.Combine(configDir, "claude-mcp.json");

            // Claude MCP config format
            var config = new
            {
                mcpServers = new
                {
                    tia_portal = new
                    {
                        command = _mcpServerCommand,
                        args = Array.Empty<string>(),
                        type = "stdio"
                    }
                }
            };

            var json = JsonSerializer.Serialize(config, s_jsonOptions);
            File.WriteAllText(_generatedMcpConfigPath, json);
            _logger.Info($"ClaudeCodeRuntime: generated MCP config at {_generatedMcpConfigPath}");
        }
        catch (Exception ex)
        {
            _logger.Error("ClaudeCodeRuntime: failed to generate MCP config", ex);
            _generatedMcpConfigPath = null;
        }
    }

    private static string EscapeShellArg(string arg)
    {
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    public void Dispose()
    {
        _processRunner.Dispose();

        // Clean up generated MCP config
        if (_generatedMcpConfigPath != null && File.Exists(_generatedMcpConfigPath))
        {
            try { File.Delete(_generatedMcpConfigPath); } catch { }
        }
    }
}
