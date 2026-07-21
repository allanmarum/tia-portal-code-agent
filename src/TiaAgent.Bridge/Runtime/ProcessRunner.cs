using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;

namespace TiaAgent.Bridge.Runtime;

/// <summary>
/// Shared process execution infrastructure for CLI-based runtime adapters.
/// Handles ProcessStartInfo setup, stdout/stderr capture, cancellation,
/// timeout, ANSI stripping, exit code handling, and orphaned process prevention.
/// </summary>
public sealed class ProcessRunner : IDisposable
{
    private readonly BridgeLogger _logger;
    private static readonly Regex AnsiEscapePattern = new Regex(
        @"\x1B\[[0-9;]*[A-Za-z]|\x1B\][^\x07]*\x07|\x1B\([AB01]",
        RegexOptions.Compiled);

    public ProcessRunner(BridgeLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes a process and captures its output.
    /// </summary>
    /// <param name="executable">Path or name of the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <param name="timeout">Maximum execution time.</param>
    /// <param name="environmentVariables">Optional environment variables to set.</param>
    /// <param name="progress">Optional progress reporter for stdout lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process result with exit code, stdout, and stderr.</returns>
    public async Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        TimeSpan timeout,
        System.Collections.Generic.Dictionary<string, string>? environmentVariables = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrEmpty(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;

            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                    startInfo.Environment[kvp.Key] = kvp.Value;
            }

            _logger.Info($"ProcessRunner: starting '{executable}' with args: {Truncate(arguments, 500)}");

            process = new Process { StartInfo = startInfo };
            process.Start();

            // Close stdin immediately to signal non-interactive mode
            try { process.StandardInput.Close(); } catch { }

            _logger.Info($"ProcessRunner: process started (PID={process.Id})");

            // Use a linked CTS for timeout + external cancellation
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            // Read stdout and stderr concurrently
            var stdoutTask = ReadStreamAsync(process.StandardOutput, stdout, progress, linkedCts.Token);
            var stderrTask = ReadStreamAsync(process.StandardError, stderr, null, linkedCts.Token);

            try
            {
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.Warn($"ProcessRunner: process timed out after {timeout.TotalSeconds}s, killing process tree");
                KillProcessTree(process);
                return new ProcessResult
                {
                    ExitCode = -1,
                    StdOut = stdout.ToString(),
                    StdErr = stderr.ToString(),
                    TimedOut = true,
                    Error = $"Process timed out after {timeout.TotalSeconds} seconds"
                };
            }
            catch (OperationCanceledException)
            {
                _logger.Info("ProcessRunner: process cancelled");
                KillProcessTree(process);
                return new ProcessResult
                {
                    ExitCode = -1,
                    StdOut = stdout.ToString(),
                    StdErr = stderr.ToString(),
                    Cancelled = true,
                    Error = "Process was cancelled"
                };
            }

            var exitCode = process.ExitCode;
            _logger.Info($"ProcessRunner: process exited with code {exitCode}");

            return new ProcessResult
            {
                ExitCode = exitCode,
                StdOut = stdout.ToString(),
                StdErr = stderr.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"ProcessRunner: failed to start process '{executable}'", ex);
            return new ProcessResult
            {
                ExitCode = -1,
                StdOut = stdout.ToString(),
                StdErr = stderr.ToString(),
                Error = $"Failed to start process: {ex.Message}"
            };
        }
        finally
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    KillProcessTree(process);
                }
            }
            catch (InvalidOperationException)
            {
                // Process was never started (e.g. executable not found)
            }
            process?.Dispose();
        }
    }

    private static async Task ReadStreamAsync(
        System.IO.StreamReader reader,
        StringBuilder buffer,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;

                lock (buffer)
                {
                    buffer.AppendLine(line);
                }

                progress?.Report(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch
        {
            // Stream may be closed if process exits
        }
    }

    /// <summary>
    /// Kills the process and its children to prevent orphaned processes.
    /// </summary>
    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort kill
            try { if (!process.HasExited) process.Kill(); } catch { }
        }
    }

    /// <summary>
    /// Strips ANSI escape sequences from text.
    /// Should be called at the presentation boundary, not during capture.
    /// </summary>
    public static string StripAnsiEscapes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return AnsiEscapePattern.Replace(text, string.Empty);
    }

    private static string Truncate(string s, int maxLength)
    {
        if (s == null) return "";
        return s.Length <= maxLength ? s : string.Concat(s.AsSpan(0, maxLength), "...");
    }

    public void Dispose()
    {
        // No persistent state to dispose
    }
}

/// <summary>
/// Result of a process execution.
/// </summary>
public sealed class ProcessResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public bool TimedOut { get; init; }
    public bool Cancelled { get; init; }
    public string? Error { get; init; }
    public bool Success => ExitCode == 0 && Error == null;
}
