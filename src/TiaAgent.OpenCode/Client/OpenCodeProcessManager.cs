using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.OpenCode.Client;

/// <summary>
/// Instance-aware process manager for OpenCode runtime server lifecycle.
/// </summary>
public class OpenCodeProcessManager : IOpenCodeProcessManager, IDisposable
{
    private readonly string _executablePath;
    private readonly string _arguments;
    private readonly string _serverUrl;
    private readonly HttpClient _httpClient;
    private readonly string _instanceId;
    private Process? _process;
    private DateTime? _processStartTime;
    private bool _disposed;

    public OpenCodeProcessManager(
        string executablePath,
        string arguments,
        string serverUrl,
        HttpClient? httpClient = null,
        string? instanceId = null)
    {
        _executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        _arguments = arguments ?? string.Empty;
        _serverUrl = serverUrl ?? "http://127.0.0.1:43120";
        _httpClient = httpClient ?? new HttpClient();
        _instanceId = instanceId ?? Guid.NewGuid().ToString("N");
    }

    public string InstanceId => _instanceId;
    public int? ProcessId => _process is { HasExited: false } ? _process.Id : null;

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        if (await IsRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var psi = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = _arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.Environment["TIA_AGENT_INSTANCE_ID"] = _instanceId;

        var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            return false;
        }

        _process = process;
        try
        {
            _processStartTime = process.StartTime;
        }
        catch
        {
            _processStartTime = DateTime.UtcNow;
        }

        // Poll for health until healthy or cancelled
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 30000)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                return false;
            }

            if (await HealthCheckAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process == null || _process.HasExited)
        {
            return;
        }

        // Validate process identity to prevent PID reuse issues
        try
        {
            var currentProcess = Process.GetProcessById(_process.Id);
            if (_processStartTime.HasValue)
            {
                try
                {
                    if (Math.Abs((currentProcess.StartTime - _processStartTime.Value).TotalSeconds) > 2)
                    {
                        // PID was reused by another process
                        _process = null;
                        return;
                    }
                }
                catch
                {
                    // Ignore error fetching currentProcess.StartTime
                }
            }
        }
        catch (ArgumentException)
        {
            // Process already exited
            _process = null;
            return;
        }

        if (_process == null)
        {
            return;
        }

        try
        {
            _process.CloseMainWindow();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            while (!_process.HasExited)
            {
                if (linkedCts.IsCancellationRequested)
                    break;

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore graceful stop exceptions
        }

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // Ignore kill errors if already terminated
            }
        }

        _process = null;
    }

    public Task<bool> IsRunningAsync(CancellationToken cancellationToken)
    {
        if (_process == null || _process.HasExited)
        {
            return Task.FromResult(false);
        }

        // Verify PID identity
        try
        {
            var currentProcess = Process.GetProcessById(_process.Id);
            if (_processStartTime.HasValue)
            {
                try
                {
                    if (Math.Abs((currentProcess.StartTime - _processStartTime.Value).TotalSeconds) > 2)
                    {
                        return Task.FromResult(false);
                    }
                }
                catch
                {
                    // Ignore start time check errors
                }
            }

            return Task.FromResult(!currentProcess.HasExited);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var healthUrl = _serverUrl.TrimEnd('/') + "/health";
            var response = await _httpClient.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
                _process.Dispose();
            }
            catch { }
        }

        GC.SuppressFinalize(this);
    }
}
