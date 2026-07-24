using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Cli.Commands;
using TiaAgent.Cli.Layout;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Supervisor;

public static class SupervisorEngine
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true
    };

    public static async Task<int> StartAsync(
        StartOptions options,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken = default)
    {
        var layout = new TiaAgentLayout(options.CustomRoot);
        layout.EnsureDirectoriesExist();

        stdout.WriteLine();
        stdout.WriteLine("======================================");
        stdout.WriteLine("  TIA Agent Runtime Supervisor");
        stdout.WriteLine("======================================");
        stdout.WriteLine();

        SupervisorLock lockHandle;
        try
        {
            stdout.WriteLine("[1/14] Acquiring supervisor mutex...");
            lockHandle = SupervisorLock.Acquire(layout);
            stdout.WriteLine($"  Instance: {lockHandle.InstanceId}");
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"FAILED: {ex.Message}");
            return 1;
        }

        using var supLock = lockHandle;
        var instanceId = lockHandle.InstanceId;
        var currentPid = Environment.ProcessId;

        Process? bridgeProcess = null;
        Process? runtimeProcess = null;

        try
        {
            // Step 2: Clean stale state
            stdout.WriteLine("[2/14] Checking for stale runtime...");
            CleanStaleRuntime(layout, instanceId);
            stdout.WriteLine("  Stale state cleaned");

            // Step 3: Load settings & runtime config
            stdout.WriteLine("[3/14] Loading config and settings...");
            var (defaultRuntime, runtimeMode) = LoadRuntimeConfig(layout);
            var (prefBridgePort, prefRuntimePort, rangeStart, rangeEnd) = LoadSettings(layout, options.Config);
            stdout.WriteLine($"  Runtime: {defaultRuntime} (mode={runtimeMode})");

            // Fail fast before startup if selected runtime is unknown
            if (!RuntimeCompatibilityRegistry.IsKnownRuntime(defaultRuntime))
            {
                var avail = string.Join(", ", RuntimeCompatibilityRegistry.KnownRuntimes.Keys.OrderBy(k => k));
                throw new InvalidOperationException($"Unknown runtime '{defaultRuntime}'. Available runtimes: {avail}");
            }

            bool runtimeNeedsServer = string.Equals(runtimeMode, "server", StringComparison.OrdinalIgnoreCase);

            // Step 4: Allocate ports
            stdout.WriteLine("[4/14] Allocating ports...");
            int bridgePort = PortAllocator.GetAvailablePort(prefBridgePort, rangeStart, rangeEnd);
            stdout.WriteLine($"  Bridge port: {bridgePort}");

            int runtimePort = 0;
            if (runtimeNeedsServer)
            {
                runtimePort = PortAllocator.GetAvailablePort(prefRuntimePort, rangeStart, rangeEnd);
                stdout.WriteLine($"  Runtime server port: {runtimePort}");
            }
            else
            {
                stdout.WriteLine("  Runtime server: not needed (CLI mode)");
            }

            // Step 5: Generate credentials
            stdout.WriteLine("[5/14] Generating credentials...");
            var secretsDir = Path.Combine(layout.RuntimePath, "secrets");
            Directory.CreateDirectory(secretsDir);
            var tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            var mcpToken = Convert.ToBase64String(tokenBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            File.WriteAllText(Path.Combine(secretsDir, "mcp.token"), mcpToken, Encoding.UTF8);
            stdout.WriteLine("  MCP token generated");

            // Step 6: Publish initial manifest
            stdout.WriteLine("[6/14] Publishing runtime manifest...");
            var manifestPath = Path.Combine(layout.RuntimePath, "runtime.json");
            var runtimeDisplayName = GetRuntimeDisplayName(defaultRuntime);

            var manifest = new RuntimeManifest
            {
                SchemaVersion = 1,
                InstanceId = instanceId,
                Status = "starting",
                SupervisorPid = currentPid,
                StartedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o"),
                Runtime = new RuntimeIdentityInfo
                {
                    Id = defaultRuntime,
                    DisplayName = runtimeDisplayName,
                    Mode = runtimeMode,
                    Healthy = false
                },
                Services = new RuntimeServicesInfo
                {
                    Bridge = new ServiceInfo
                    {
                        Status = "starting",
                        Host = "127.0.0.1",
                        Port = bridgePort,
                        BaseUrl = $"http://127.0.0.1:{bridgePort}",
                        HealthUrl = $"http://127.0.0.1:{bridgePort}/health"
                    },
                    OpenCode = new ServiceInfo
                    {
                        Status = runtimeNeedsServer ? "pending" : "skipped",
                        Host = "127.0.0.1",
                        Port = runtimePort,
                        BaseUrl = runtimeNeedsServer ? $"http://127.0.0.1:{runtimePort}" : string.Empty,
                        HealthUrl = runtimeNeedsServer ? $"http://127.0.0.1:{runtimePort}/health" : string.Empty
                    }
                }
            };
            ManifestStore.WriteAtomic(manifestPath, manifest);
            stdout.WriteLine($"  Manifest published (status: starting)");

            // Step 7: Locate Bridge binary & write bridge.json
            stdout.WriteLine("[7/14] Locating Bridge binary...");
            var bridgePath = LocateBridgeBinary(layout, options.RepoRoot);
            stdout.WriteLine($"  Bridge path: {bridgePath}");

            var bridgeConfig = new Dictionary<string, object>
            {
                ["port"] = bridgePort,
                ["openCodeBaseUrl"] = runtimeNeedsServer ? $"http://127.0.0.1:{runtimePort}" : string.Empty,
                ["taskTimeoutSeconds"] = 300,
                ["maxConcurrentTasks"] = 5,
                ["maxRequestBodyBytes"] = 1048576
            };
            var bridgeConfigJson = JsonSerializer.Serialize(bridgeConfig, s_jsonOptions);
            File.WriteAllText(Path.Combine(layout.RootPath, "bridge.json"), bridgeConfigJson, Encoding.UTF8);

            // Step 8: Start Bridge process
            stdout.WriteLine("[8/14] Starting Bridge...");
            var bridgeLog = Path.Combine(layout.LogsPath, "bridge.log");
            bridgeProcess = StartProcess(bridgePath, bridgeLog, instanceId, options.RepoRoot);
            manifest.Services.Bridge.Pid = bridgeProcess.Id;
            ManifestStore.WriteAtomic(manifestPath, manifest);
            stdout.WriteLine($"  Bridge started (PID: {bridgeProcess.Id})");

            // Step 9: Wait for Bridge health
            stdout.WriteLine("[9/14] Waiting for Bridge health...");
            bool bridgeHealthy = await HealthChecker.WaitUntilHealthyAsync(
                manifest.Services.Bridge.HealthUrl,
                timeoutSeconds: 30,
                retryIntervalMs: 500,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!bridgeHealthy)
            {
                manifest.Status = "failed";
                manifest.Services.Bridge.Status = "failed";
                ManifestStore.WriteAtomic(manifestPath, manifest);
                throw new InvalidOperationException("Bridge health check failed.");
            }

            manifest.Services.Bridge.Status = "healthy";
            ManifestStore.WriteAtomic(manifestPath, manifest);
            stdout.WriteLine("  Bridge healthy");

            // Step 10: Start Runtime Server if needed
            if (runtimeNeedsServer)
            {
                stdout.WriteLine($"[10/14] Starting runtime server ({defaultRuntime})...");
                var runtimeLog = Path.Combine(layout.LogsPath, $"{defaultRuntime}.log");
                runtimeProcess = StartRuntimeServer(defaultRuntime, runtimePort, layout, runtimeLog, instanceId);
                if (runtimeProcess != null)
                {
                    manifest.Services.OpenCode.Pid = runtimeProcess.Id;
                    manifest.Services.OpenCode.Status = "starting";
                    ManifestStore.WriteAtomic(manifestPath, manifest);
                    stdout.WriteLine($"  Runtime server started (PID: {runtimeProcess.Id})");
                }

                // Step 11: Wait for Runtime health
                stdout.WriteLine("[11/14] Waiting for runtime server health...");
                bool runtimeHealthy = await HealthChecker.WaitUntilHealthyAsync(
                    manifest.Services.OpenCode.HealthUrl,
                    timeoutSeconds: 30,
                    retryIntervalMs: 500,
                    tcpPortFallback: runtimePort,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!runtimeHealthy)
                {
                    manifest.Status = "failed";
                    manifest.Services.OpenCode.Status = "failed";
                    ManifestStore.WriteAtomic(manifestPath, manifest);
                    throw new InvalidOperationException($"Runtime server health check failed for '{defaultRuntime}'.");
                }

                manifest.Services.OpenCode.Status = "healthy";
                if (manifest.Runtime != null) manifest.Runtime.Healthy = true;
                ManifestStore.WriteAtomic(manifestPath, manifest);
                stdout.WriteLine("  Runtime server healthy");
            }
            else
            {
                stdout.WriteLine("[10/14] Skipping runtime server (CLI mode)");
                stdout.WriteLine("[11/14] Skipping server health check (CLI mode)");
                if (manifest.Runtime != null) manifest.Runtime.Healthy = true;
            }

            // Step 12: Publish ready status
            stdout.WriteLine("[12/14] Publishing ready status...");
            manifest.Status = "ready";
            manifest.UpdatedAt = DateTime.UtcNow.ToString("o");
            ManifestStore.WriteAtomic(manifestPath, manifest);
            stdout.WriteLine("  Runtime status: ready");

            // Step 13: Display summary
            stdout.WriteLine();
            stdout.WriteLine("======================================");
            stdout.WriteLine("  Runtime Ready");
            stdout.WriteLine("======================================");
            stdout.WriteLine();
            stdout.WriteLine($"Instance   : {instanceId}");
            stdout.WriteLine($"Status     : Ready");
            stdout.WriteLine($"Supervisor : Running, PID {currentPid}");
            stdout.WriteLine($"Bridge     : Healthy, http://127.0.0.1:{bridgePort}");
            stdout.WriteLine($"Runtime    : {defaultRuntime} ({runtimeDisplayName}, mode={runtimeMode})");
            if (runtimeNeedsServer)
            {
                stdout.WriteLine($"Server     : Healthy, http://127.0.0.1:{runtimePort}");
            }
            else
            {
                stdout.WriteLine($"Server     : Not needed (CLI mode)");
            }
            stdout.WriteLine();
            stdout.WriteLine($"Runtime manifest: {manifestPath}");
            stdout.WriteLine();

            // Step 14: Monitoring loop
            if (options.NoMonitor)
            {
                stdout.WriteLine("Exiting (NoMonitor mode)...");
                return 0;
            }

            stdout.WriteLine("Monitoring services (Ctrl+C to stop)...");
            var supLogPath = Path.Combine(layout.LogsPath, "supervisor.log");

            while (!cancellationToken.IsCancellationRequested)
            {
                if (bridgeProcess != null && bridgeProcess.HasExited)
                {
                    LogEvent(supLogPath, instanceId, "ERROR", "bridge_exited", $"Bridge exited with code {bridgeProcess.ExitCode}");
                    manifest.Status = "degraded";
                    manifest.Services.Bridge.Status = "failed";
                    ManifestStore.WriteAtomic(manifestPath, manifest);
                    stdout.WriteLine($"  WARNING: Bridge process exited with code {bridgeProcess.ExitCode}");
                }

                if (runtimeProcess != null && runtimeProcess.HasExited)
                {
                    LogEvent(supLogPath, instanceId, "ERROR", "runtime_exited", $"Runtime server exited with code {runtimeProcess.ExitCode}");
                    manifest.Status = "degraded";
                    manifest.Services.OpenCode.Status = "failed";
                    if (manifest.Runtime != null) manifest.Runtime.Healthy = false;
                    ManifestStore.WriteAtomic(manifestPath, manifest);
                    stdout.WriteLine($"  WARNING: Runtime server process exited with code {runtimeProcess.ExitCode}");
                }

                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            stdout.WriteLine();
            stdout.WriteLine("Shutdown requested...");
            return 0;
        }
        catch (Exception ex)
        {
            stderr.WriteLine();
            stderr.WriteLine($"FAILED: {ex.Message}");
            LogEvent(Path.Combine(layout.LogsPath, "supervisor.log"), instanceId, "ERROR", "startup_error", ex.Message);

            try
            {
                var manifestPath = Path.Combine(layout.RuntimePath, "runtime.json");
                if (File.Exists(manifestPath))
                {
                    var manifest = ManifestStore.Read<RuntimeManifest>(manifestPath);
                    manifest.Status = "failed";
                    manifest.UpdatedAt = DateTime.UtcNow.ToString("o");
                    ManifestStore.WriteAtomic(manifestPath, manifest);
                }
            }
            catch { }

            return 1;
        }
        finally
        {
            // Cleanup on shutdown
            stdout.WriteLine("Cleaning up services...");
            StopProcesses(bridgeProcess, runtimeProcess);

            try
            {
                var manifestPath = Path.Combine(layout.RuntimePath, "runtime.json");
                if (File.Exists(manifestPath))
                {
                    var manifest = ManifestStore.Read<RuntimeManifest>(manifestPath);
                    manifest.Status = "stopped";
                    manifest.UpdatedAt = DateTime.UtcNow.ToString("o");
                    ManifestStore.WriteAtomic(manifestPath, manifest);
                }
            }
            catch { }

            CleanSecrets(layout);
        }
    }

    public static int Stop(
        StopOptions options,
        TextWriter stdout,
        TextWriter stderr)
    {
        var layout = new TiaAgentLayout(options.CustomRoot);
        var manifestPath = Path.Combine(layout.RuntimePath, "runtime.json");

        stdout.WriteLine();
        stdout.WriteLine("======================================");
        stdout.WriteLine("  TIA Agent Runtime Shutdown");
        stdout.WriteLine("======================================");
        stdout.WriteLine();

        if (!File.Exists(manifestPath))
        {
            stdout.WriteLine("No runtime manifest found. Nothing to stop.");
            return 0;
        }

        RuntimeManifest manifest;
        try
        {
            manifest = ManifestStore.Read<RuntimeManifest>(manifestPath);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Failed to parse runtime manifest: {ex.Message}");
            return 1;
        }

        if (string.Equals(manifest.Status, "stopped", StringComparison.OrdinalIgnoreCase))
        {
            stdout.WriteLine("Runtime already stopped.");
            return 0;
        }

        stdout.WriteLine($"Instance: {manifest.InstanceId}");
        stdout.WriteLine($"Status  : {manifest.Status}");

        manifest.Status = "stopping";
        manifest.UpdatedAt = DateTime.UtcNow.ToString("o");
        ManifestStore.WriteAtomic(manifestPath, manifest);

        // Stop runtime server first
        if (manifest.Services.OpenCode.Pid > 0)
        {
            stdout.WriteLine($"Stopping OpenCode (PID: {manifest.Services.OpenCode.Pid})...");
            StopProcessById(manifest.Services.OpenCode.Pid, options.Force);
        }

        // Stop Bridge
        if (manifest.Services.Bridge.Pid > 0)
        {
            stdout.WriteLine($"Stopping Bridge (PID: {manifest.Services.Bridge.Pid})...");
            StopProcessById(manifest.Services.Bridge.Pid, options.Force);
        }

        // Clean secrets and lock
        CleanSecrets(layout);
        var lockFilePath = Path.Combine(layout.RuntimePath, "supervisor.lock");
        if (File.Exists(lockFilePath))
        {
            try { File.Delete(lockFilePath); } catch { }
        }

        manifest.Status = "stopped";
        manifest.UpdatedAt = DateTime.UtcNow.ToString("o");
        ManifestStore.WriteAtomic(manifestPath, manifest);

        stdout.WriteLine();
        stdout.WriteLine("======================================");
        stdout.WriteLine("  Runtime Stopped");
        stdout.WriteLine("======================================");
        stdout.WriteLine();
        return 0;
    }

    public static int GetStatus(
        StatusOptions options,
        TextWriter stdout,
        TextWriter stderr)
    {
        return GetStatusAsync(options, stdout, stderr).GetAwaiter().GetResult();
    }

    public static async Task<int> GetStatusAsync(
        StatusOptions options,
        TextWriter stdout,
        TextWriter stderr)
    {
        var layout = new TiaAgentLayout(options.CustomRoot);
        var manifestPath = Path.Combine(layout.RuntimePath, "runtime.json");

        RuntimeManifest? manifest = null;
        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = ManifestStore.Read<RuntimeManifest>(manifestPath);
            }
            catch (Exception ex)
            {
                if (options.Json)
                {
                    var errObj = new { error = $"Failed to parse runtime manifest: {ex.Message}" };
                    stdout.WriteLine(JsonSerializer.Serialize(errObj, s_jsonOptions));
                }
                else
                {
                    stderr.WriteLine($"Failed to parse runtime manifest: {ex.Message}");
                }
                return 1;
            }
        }

        bool supervisorRunning = false;
        bool bridgeRunning = false;
        bool bridgeHealthy = false;
        bool opencodeRunning = false;
        bool opencodeHealthy = false;

        if (manifest != null)
        {
            if (manifest.SupervisorPid > 0)
            {
                try
                {
                    using var proc = Process.GetProcessById(manifest.SupervisorPid);
                    supervisorRunning = !proc.HasExited;
                }
                catch { }
            }

            if (manifest.Services.Bridge.Pid > 0)
            {
                try
                {
                    using var proc = Process.GetProcessById(manifest.Services.Bridge.Pid);
                    bridgeRunning = !proc.HasExited;
                }
                catch { }

                if (bridgeRunning && !string.IsNullOrEmpty(manifest.Services.Bridge.HealthUrl))
                {
                    bridgeHealthy = await HealthChecker.IsHealthyAsync(manifest.Services.Bridge.HealthUrl).ConfigureAwait(false);
                }
            }

            if (manifest.Services.OpenCode.Pid > 0)
            {
                try
                {
                    using var proc = Process.GetProcessById(manifest.Services.OpenCode.Pid);
                    opencodeRunning = !proc.HasExited;
                }
                catch { }

                if (opencodeRunning && !string.IsNullOrEmpty(manifest.Services.OpenCode.HealthUrl))
                {
                    opencodeHealthy = await HealthChecker.IsHealthyAsync(manifest.Services.OpenCode.HealthUrl, manifest.Services.OpenCode.Port).ConfigureAwait(false);
                }
            }
        }

        var result = new StatusResult
        {
            InstanceId = manifest?.InstanceId ?? string.Empty,
            Status = manifest?.Status ?? "unknown",
            Supervisor = new SupervisorStatusInfo
            {
                Running = supervisorRunning,
                Pid = manifest?.SupervisorPid ?? 0
            },
            Bridge = new ServiceStatusInfo
            {
                Running = bridgeRunning,
                Healthy = bridgeHealthy,
                Pid = manifest?.Services.Bridge.Pid ?? 0,
                Host = manifest?.Services.Bridge.Host ?? "127.0.0.1",
                Port = manifest?.Services.Bridge.Port ?? 0,
                Url = manifest?.Services.Bridge.BaseUrl ?? string.Empty
            },
            OpenCode = new ServiceStatusInfo
            {
                Running = opencodeRunning,
                Healthy = opencodeHealthy,
                Pid = manifest?.Services.OpenCode.Pid ?? 0,
                Host = manifest?.Services.OpenCode.Host ?? "127.0.0.1",
                Port = manifest?.Services.OpenCode.Port ?? 0,
                Url = manifest?.Services.OpenCode.BaseUrl ?? string.Empty
            },
            RuntimePath = manifestPath
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(result, s_jsonOptions));
        }
        else
        {
            stdout.WriteLine();
            stdout.WriteLine("TIA Agent Runtime");
            stdout.WriteLine();
            var instanceShort = !string.IsNullOrEmpty(result.InstanceId)
                ? result.InstanceId[..Math.Min(8, result.InstanceId.Length)]
                : "N/A";
            stdout.WriteLine($"Instance   : {instanceShort}");
            stdout.WriteLine($"Status     : {result.Status}");
            stdout.WriteLine($"Supervisor : {(result.Supervisor.Running ? $"Running, PID {result.Supervisor.Pid}" : "Not running")}");
            stdout.WriteLine($"Bridge     : {(result.Bridge.Running && result.Bridge.Healthy ? $"Healthy, {result.Bridge.Url}" : result.Bridge.Running ? $"Running (not healthy), PID {result.Bridge.Pid}" : "Not running")}");
            stdout.WriteLine($"Runtime    : {(result.OpenCode.Running && result.OpenCode.Healthy ? $"Healthy, {result.OpenCode.Url}" : result.OpenCode.Running ? $"Running (not healthy), PID {result.OpenCode.Pid}" : "Not running")}");

            if (manifest?.Runtime != null)
            {
                stdout.WriteLine($"           : {manifest.Runtime.DisplayName} (mode={manifest.Runtime.Mode}, healthy={(manifest.Runtime.Healthy ? "yes" : "no")})");
            }
            stdout.WriteLine();
            stdout.WriteLine("Runtime manifest:");
            stdout.WriteLine(manifestPath);
            stdout.WriteLine();
        }

        return 0;
    }

    private static (string DefaultRuntime, string RuntimeMode) LoadRuntimeConfig(TiaAgentLayout layout)
    {
        var configPath = layout.ConfigPath;
        var defaultRuntime = "opencode";
        var runtimeMode = "server";

        var envRuntime = Environment.GetEnvironmentVariable("TIA_AGENT_RUNTIME");
        if (!string.IsNullOrWhiteSpace(envRuntime))
        {
            defaultRuntime = envRuntime.Trim();
        }
        else if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("defaultRuntime", out var defProp) && defProp.ValueKind == JsonValueKind.String)
                {
                    defaultRuntime = defProp.GetString() ?? "opencode";
                }

                if (doc.RootElement.TryGetProperty("runtimes", out var runtimesProp) &&
                    runtimesProp.TryGetProperty(defaultRuntime, out var rtProp) &&
                    rtProp.TryGetProperty("mode", out var modeProp) &&
                    modeProp.ValueKind == JsonValueKind.String)
                {
                    runtimeMode = modeProp.GetString() ?? "cli";
                }
                else if (defaultRuntime == "opencode")
                {
                    runtimeMode = "server";
                }
                else
                {
                    runtimeMode = "cli";
                }
            }
            catch { }
        }

        if (RuntimeCompatibilityRegistry.GetMetadata(defaultRuntime) != null)
        {
            var meta = RuntimeCompatibilityRegistry.GetMetadata(defaultRuntime)!;
            if (!meta.SupportedModes.Contains(runtimeMode))
            {
                runtimeMode = meta.DefaultMode;
            }
        }

        return (defaultRuntime, runtimeMode);
    }

    private static (int PreferredBridgePort, int PreferredRuntimePort, int RangeStart, int RangeEnd) LoadSettings(TiaAgentLayout layout, string? customConfigPath)
    {
        var path = !string.IsNullOrWhiteSpace(customConfigPath)
            ? customConfigPath
            : Path.Combine(layout.RootPath, "config", "settings.json");

        int prefBridge = PortAllocator.DefaultBridgePort;
        int prefRuntime = PortAllocator.DefaultRuntimePort;
        int rangeStart = PortAllocator.DefaultRangeStart;
        int rangeEnd = PortAllocator.DefaultRangeEnd;

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("preferredPorts", out var portsProp))
                {
                    if (portsProp.TryGetProperty("bridge", out var bProp) && bProp.TryGetInt32(out var bVal)) prefBridge = bVal;
                    if (portsProp.TryGetProperty("opencode", out var oProp) && oProp.TryGetInt32(out var oVal)) prefRuntime = oVal;
                }
                if (doc.RootElement.TryGetProperty("portRange", out var rangeProp))
                {
                    if (rangeProp.TryGetProperty("start", out var sProp) && sProp.TryGetInt32(out var sVal)) rangeStart = sVal;
                    if (rangeProp.TryGetProperty("end", out var eProp) && eProp.TryGetInt32(out var eVal)) rangeEnd = eVal;
                }
            }
            catch { }
        }

        return (prefBridge, prefRuntime, rangeStart, rangeEnd);
    }

    private static string GetRuntimeDisplayName(string runtimeId) => runtimeId switch
    {
        "mimo" => "Mimo CLI",
        "opencode" => "OpenCode",
        "claude" => "Claude Code CLI",
        _ => runtimeId
    };

    private static string LocateBridgeBinary(TiaAgentLayout layout, string? repoRoot)
    {
        // 1. Check current installation version layout
        if (File.Exists(layout.CurrentManifestPath))
        {
            try
            {
                var current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
                if (!string.IsNullOrWhiteSpace(current.ActiveVersion))
                {
                    var versionPath = layout.GetVersionPath(current.ActiveVersion);
                    var installedDll = Path.Combine(versionPath, "bridge", "TiaAgent.Bridge.dll");
                    if (File.Exists(installedDll)) return installedDll;
                    var installedExe = Path.Combine(versionPath, "bridge", "TiaAgent.Bridge.exe");
                    if (File.Exists(installedExe)) return installedExe;
                }
            }
            catch { }
        }

        // 2. Check repo root if passed or detected
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            var relDll = Path.Combine(repoRoot, "src", "TiaAgent.Bridge", "bin", "Release", "net8.0", "TiaAgent.Bridge.dll");
            if (File.Exists(relDll)) return relDll;
            var dbgDll = Path.Combine(repoRoot, "src", "TiaAgent.Bridge", "bin", "Debug", "net8.0", "TiaAgent.Bridge.dll");
            if (File.Exists(dbgDll)) return dbgDll;
        }

        // 3. Check relative to CLI application directory
        var baseDir = AppContext.BaseDirectory;
        var nextToCli = Path.Combine(baseDir, "TiaAgent.Bridge.dll");
        if (File.Exists(nextToCli)) return nextToCli;

        var repoDevRel = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "TiaAgent.Bridge", "bin", "Release", "net8.0", "TiaAgent.Bridge.dll"));
        if (File.Exists(repoDevRel)) return repoDevRel;

        var repoDevDbg = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "TiaAgent.Bridge", "bin", "Debug", "net8.0", "TiaAgent.Bridge.dll"));
        if (File.Exists(repoDevDbg)) return repoDevDbg;

        return nextToCli;
    }

    private static Process StartProcess(string executableOrDll, string logFile, string instanceId, string? workingDir)
    {
        var psi = new ProcessStartInfo();
        if (executableOrDll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            psi.Arguments = $"exec \"{executableOrDll}\"";
        }
        else
        {
            psi.FileName = executableOrDll;
        }

        psi.WorkingDirectory = !string.IsNullOrWhiteSpace(workingDir) ? workingDir : Directory.GetCurrentDirectory();
        psi.EnvironmentVariables["TIA_AGENT_INSTANCE_ID"] = instanceId;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
        psi.StandardErrorEncoding = System.Text.Encoding.UTF8;
        psi.CreateNoWindow = true;

        var proc = new Process { StartInfo = psi };
        proc.Start();

        // Async log redirection
        Task.Run(async () =>
        {
            try
            {
                using var writer = new StreamWriter(new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                while (!proc.HasExited)
                {
                    var line = await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line != null) await writer.WriteLineAsync(line).ConfigureAwait(false);
                    var errLine = await proc.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (errLine != null) await writer.WriteLineAsync(errLine).ConfigureAwait(false);
                }
            }
            catch { }
        });

        return proc;
    }

    private static Process? StartRuntimeServer(string runtimeId, int port, TiaAgentLayout layout, string logFile, string instanceId)
    {
        var exeName = runtimeId == "opencode" ? "mimo" : runtimeId;
        var psi = new ProcessStartInfo
        {
            FileName = exeName,
            Arguments = $"serve --port {port}",
            WorkingDirectory = Path.Combine(layout.RuntimePath, $"{runtimeId}-workdir"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };
        Directory.CreateDirectory(psi.WorkingDirectory);
        psi.EnvironmentVariables["TIA_AGENT_INSTANCE_ID"] = instanceId;

        try
        {
            var proc = new Process { StartInfo = psi };
            proc.Start();

            Task.Run(async () =>
            {
                try
                {
                    using var writer = new StreamWriter(new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                    while (!proc.HasExited)
                    {
                        var line = await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                        if (line != null) await writer.WriteLineAsync(line).ConfigureAwait(false);
                        var errLine = await proc.StandardError.ReadLineAsync().ConfigureAwait(false);
                        if (errLine != null) await writer.WriteLineAsync(errLine).ConfigureAwait(false);
                    }
                }
                catch { }
            });

            return proc;
        }
        catch
        {
            return null;
        }
    }

    private static void StopProcessById(int pid, bool force)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            if (!proc.HasExited)
            {
                if (force)
                {
                    proc.Kill();
                }
                else
                {
                    proc.CloseMainWindow();
                    if (!proc.WaitForExit(5000))
                    {
                        proc.Kill();
                    }
                }
            }
        }
        catch { }
    }

    private static void StopProcesses(Process? bridge, Process? runtime)
    {
        if (runtime != null && !runtime.HasExited)
        {
            try { runtime.Kill(); } catch { }
        }
        if (bridge != null && !bridge.HasExited)
        {
            try { bridge.Kill(); } catch { }
        }
    }

    private static void CleanSecrets(TiaAgentLayout layout)
    {
        var secretsDir = Path.Combine(layout.RuntimePath, "secrets");
        if (Directory.Exists(secretsDir))
        {
            foreach (var file in Directory.GetFiles(secretsDir))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private static void CleanStaleRuntime(TiaAgentLayout layout, string instanceId)
    {
        var manifestPath = Path.Combine(layout.RuntimePath, "runtime.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = ManifestStore.Read<RuntimeManifest>(manifestPath);
                if (manifest.Services.Bridge.Pid > 0)
                {
                    try { using var proc = Process.GetProcessById(manifest.Services.Bridge.Pid); }
                    catch { manifest.Services.Bridge.Pid = 0; manifest.Services.Bridge.Status = "stopped"; }
                }
                if (manifest.Services.OpenCode.Pid > 0)
                {
                    try { using var proc = Process.GetProcessById(manifest.Services.OpenCode.Pid); }
                    catch { manifest.Services.OpenCode.Pid = 0; manifest.Services.OpenCode.Status = "stopped"; }
                }
                manifest.Status = "stopped";
                ManifestStore.WriteAtomic(manifestPath, manifest);
            }
            catch { }
        }
    }

    private static void LogEvent(string logPath, string instanceId, string level, string eventName, string message)
    {
        try
        {
            var logEntry = $"{DateTime.UtcNow:o} [{level}] [{instanceId}] {eventName}: {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry, Encoding.UTF8);
        }
        catch { }
    }
}
