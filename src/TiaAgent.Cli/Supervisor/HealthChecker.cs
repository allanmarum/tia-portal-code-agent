using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TiaAgent.Cli.Supervisor;

public static class HealthChecker
{
    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static async Task<bool> IsHealthyAsync(string healthUrl, int tcpPortFallback = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(healthUrl)) return false;

        try
        {
            var response = await s_httpClient.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content)) return true;

                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("status", out var statusProp))
                    {
                        var statusStr = statusProp.GetString()?.ToLowerInvariant();
                        if (statusStr == "healthy" || statusStr == "ok") return true;
                    }
                }
                catch
                {
                    // Body not JSON, HTTP 2xx status code is sufficient
                    return true;
                }

                return true;
            }
            else if ((int)response.StatusCode >= 500 && tcpPortFallback > 0)
            {
                // Fallback TCP port check for non-2xx responses (like mimo serve 503 when Web UI is absent)
                return await IsTcpPortOpenAsync(tcpPortFallback, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (tcpPortFallback > 0)
            {
                return await IsTcpPortOpenAsync(tcpPortFallback, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public static async Task<bool> WaitUntilHealthyAsync(
        string healthUrl,
        int timeoutSeconds = 30,
        int retryIntervalMs = 500,
        int tcpPortFallback = 0,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await IsHealthyAsync(healthUrl, tcpPortFallback, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            try
            {
                await Task.Delay(retryIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return false;
    }

    private static async Task<bool> IsTcpPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            var delayTask = Task.Delay(1000, cancellationToken);
            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);

            if (completed == connectTask && client.Connected)
            {
                return true;
            }
        }
        catch { }

        return false;
    }
}
