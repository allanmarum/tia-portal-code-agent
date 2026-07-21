using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Api;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Tasks;
using TiaAgent.Contracts.Runtime;

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
        logger.Startup($"Auth token fingerprint: {TokenFingerprint(tokenProvider.Token)}");

        // Load runtime configuration
        var configLoader = new RuntimeConfigLoader(logger);
        var runtimeConfig = configLoader.Load();

        // Create and populate the runtime registry
        var runtimeRegistry = new RuntimeRegistry(runtimeConfig, logger);

        // Register all known runtime adapters
        RegisterRuntimes(runtimeRegistry, runtimeConfig, config, logger);

        // Log registered runtimes
        var allRuntimes = runtimeRegistry.GetAllRuntimes();
        logger.Startup($"Registered runtimes: {string.Join(", ", allRuntimes.Select(r => $"{r.Id} ({r.DisplayName})"))}");
        logger.Startup($"Default runtime: {runtimeRegistry.GetDefaultRuntimeId()}");

        // Check availability of all runtimes
        logger.Startup("Checking runtime availability...");
        var availability = await runtimeRegistry.CheckAllAvailabilityAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var kvp in availability)
        {
            var status = kvp.Value.Available ? "available" : "unavailable";
            var detail = kvp.Value.Available
                ? $"version={kvp.Value.Version}, mode={kvp.Value.Mode}"
                : $"error={kvp.Value.Error}";
            logger.Startup($"  {kvp.Key}: {status} ({detail})");
        }

        // Create task manager with runtime registry
        var taskManager = new TaskManager(runtimeRegistry, config.MaxConcurrentTasks, logger);

        // Create and start the controller
        var controller = new BridgeController(config, logger, tokenProvider, runtimeRegistry, taskManager);

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
            logger.Startup("Endpoints:");
            logger.Startup("  GET  /health");
            logger.Startup("  POST /v1/tasks");
            logger.Startup("  GET  /v1/tasks/{taskId}");
            logger.Startup("  POST /v1/tasks/{taskId}/cancel");
            logger.Startup("  GET  /api/runtimes");
            logger.Startup("  GET  /api/runtimes/{id}/health");
            logger.Startup("  GET  /api/settings/runtime");
            logger.Startup("  PUT  /api/settings/runtime");
            logger.Startup("  GET  /diagnostics");
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
            runtimeRegistry.Dispose();
            logger.Info("Bridge stopped");
        }
    }

    /// <summary>
    /// Registers all known runtime adapters based on configuration.
    /// Only registers enabled runtimes.
    /// </summary>
    private static void RegisterRuntimes(
        RuntimeRegistry registry,
        TiaAgentConfig runtimeConfig,
        BridgeConfig bridgeConfig,
        BridgeLogger logger)
    {
        // Mimo CLI — always register (check availability later)
        var mimoConfig = GetRuntimeEntry(runtimeConfig, "mimo");
        if (mimoConfig?.Enabled != false)
        {
            var mimoRuntime = new MimoCliRuntime(
                logger,
                executable: mimoConfig?.Executable,
                model: null);
            registry.Register(mimoRuntime);
        }

        // OpenCode — supports server and CLI modes
        var opencodeConfig = GetRuntimeEntry(runtimeConfig, "opencode");
        if (opencodeConfig?.Enabled != false)
        {
            var mode = opencodeConfig?.Mode ?? "server";
            var serverUrl = opencodeConfig?.ServerUrl ?? $"http://127.0.0.1:{bridgeConfig.Port + 1}";

            var opencodeRuntime = new OpenCodeRuntime(
                logger,
                mode: mode,
                serverUrl: serverUrl,
                executable: opencodeConfig?.Executable,
                model: null);
            registry.Register(opencodeRuntime);
        }

        // Claude Code CLI
        var claudeConfig = GetRuntimeEntry(runtimeConfig, "claude");
        if (claudeConfig?.Enabled != false)
        {
            // Find tia-mcp for MCP config generation.
            // Prefer the .NET global tools copy (stable path) over bare name.
            // Do NOT spawn tia-mcp to test --version: it is a stdio MCP server
            // that blocks reading stdin, causing Process.Start + WaitForExit to
            // hang until the timeout expires. File-existence check is sufficient.
            string? mcpCommand = null;
            var dotnetToolsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools");

            if (Directory.Exists(dotnetToolsDir))
            {
                var mcpExePath = Path.Combine(dotnetToolsDir, "tia-mcp.exe");
                if (File.Exists(mcpExePath))
                {
                    mcpCommand = mcpExePath;
                }
            }

            if (mcpCommand == null)
            {
                // Fallback: check if tia-mcp is on PATH (bare name)
                var pathVar = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathVar))
                {
                    foreach (var dir in pathVar.Split(Path.PathSeparator))
                    {
                        var candidate = Path.Combine(dir.Trim(), "tia-mcp.exe");
                        if (File.Exists(candidate))
                        {
                            mcpCommand = candidate;
                            break;
                        }
                    }
                }
            }

            logger.Info($"RegisterRuntimes: tia-mcp {(mcpCommand != null ? $"found at '{mcpCommand}'" : "not found")}");

            var claudeRuntime = new ClaudeCodeRuntime(
                logger,
                executable: claudeConfig?.Executable,
                model: null,
                mcpServerCommand: mcpCommand);
            registry.Register(claudeRuntime);
        }
    }

    private static RuntimeEntryConfig? GetRuntimeEntry(TiaAgentConfig config, string id)
    {
        return config.Runtimes.TryGetValue(id, out var entry) ? entry : null;
    }
}
