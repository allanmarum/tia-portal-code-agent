using System;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Api;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Sessions;
using TiaAgent.Bridge.Tasks;

namespace TiaAgent.Bridge;

public static class Program
{
    private static string TokenFingerprint(string token)
    {
        if (string.IsNullOrEmpty(token)) return "<empty>";
        return token.Length > 8
            ? $"{token[..4]}...{token[^4..]} ({token.Length} chars)"
            : $"{token[..2]}... ({token.Length} chars)";
    }

    public static async Task Main(string[] args)
    {
        var logger = new BridgeLogger();
        var config = BridgeConfig.Load();
        var tokenProvider = new TokenProvider();

        logger.Startup("=== TIA Agent Bridge starting ===");
        logger.Startup($"Port: {config.Port}");
        logger.Startup($"OpenCode URL: {config.OpenCodeBaseUrl}");
        logger.Startup($"Max concurrent tasks: {config.MaxConcurrentTasks}");
        logger.Startup($"Task timeout: {config.TaskTimeoutSeconds}s");
        logger.Startup($"Auth token fingerprint: {TokenFingerprint(tokenProvider.Token)}");

        var openCodeClient = new OpenCodeClient(
            config.OpenCodeBaseUrl,
            TimeSpan.FromSeconds(config.TaskTimeoutSeconds));
        var sessionManager = new SessionManager(openCodeClient);
        var taskManager = new TaskManager(openCodeClient, config.MaxConcurrentTasks, logger);

        var controller = new BridgeController(config, logger, tokenProvider, openCodeClient, sessionManager, taskManager);

        var shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
            logger.Info("Shutdown signal received");
        };

        try
        {
            controller.Start();
            logger.Startup($"Bridge listening on http://127.0.0.1:{config.Port}/");
            logger.Startup("Press Ctrl+C to stop");

            await Task.Delay(Timeout.Infinite, shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            logger.Info("Shutting down...");
            controller.Stop();
            controller.Dispose();
            taskManager.Dispose();
            sessionManager.Dispose();
            openCodeClient.Dispose();
            logger.Info("Bridge stopped");
        }
    }
}
