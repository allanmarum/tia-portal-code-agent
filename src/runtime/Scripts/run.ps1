#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Starts and manages all runtime services.

.DESCRIPTION
    Idempotent bootstrap and supervisor for TIA Portal Code Agent services.
    Starts Bridge and the selected runtime, manages ports, publishes runtime
    manifest, and monitors child processes.

.PARAMETER Config
    Path to settings.json (defaults to %LOCALAPPDATA%\TiaAgent\config\settings.json)

.PARAMETER RepoRoot
    Repository root path (auto-detected if not specified)

.PARAMETER NoMonitor
    Start services but do not monitor (exit after health checks pass)

.PARAMETER Verbose
    Enable verbose logging output

.EXAMPLE
    .\run.ps1
    .\run.ps1 -Verbose
    .\run.ps1 -NoMonitor
#>
[CmdletBinding()]
param(
    [string]$Config = '',
    [string]$RepoRoot = '',
    [switch]$NoMonitor
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = $PSScriptRoot
$ModuleRoot = Split-Path $ScriptRoot -Parent

# Import module
Import-Module (Join-Path $ModuleRoot 'TiaAgent.Supervisor.psd1') -Force

# Auto-detect repo root: Scripts -> runtime -> src -> repo
if (-not $RepoRoot) {
    $RepoRoot = (Get-Item $ScriptRoot).Parent.Parent.Parent.FullName
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  TIA Agent Runtime Supervisor" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Acquire supervisor mutex
Write-Host "[1/16] Acquiring supervisor mutex..." -ForegroundColor Yellow
$lock = Lock-TiaAgentSupervisor
$instanceId = $lock.InstanceId
Write-Host "  Instance: $instanceId" -ForegroundColor Green

# Set up cleanup on exit
$supervisorPid = $PID
$bridgeProcess = $null
$runtimeProcess = $null
$shutdownRequested = $false

$cleanup = {
    param($InstanceId, $TiaAgentRoot, $Mutex, $LockFile, $BridgeProcess, $RuntimeProcess)

    Write-TiaAgentLog -Level 'INFO' -InstanceId $InstanceId -Event 'shutdown_start' -Message "Supervisor shutting down..."

    # Stop services
    if ($BridgeProcess -and -not $BridgeProcess.HasExited) {
        Stop-TiaAgentService -Process $BridgeProcess -ServiceName 'bridge' -RuntimeInstanceId $InstanceId
    }
    if ($RuntimeProcess -and -not $RuntimeProcess.HasExited) {
        Stop-TiaAgentService -Process $RuntimeProcess -ServiceName 'runtime' -RuntimeInstanceId $InstanceId
    }

    # Update manifest
    try {
        New-TiaAgentRuntimeManifest -InstanceId $InstanceId -Status 'stopped' -TiaAgentRoot $TiaAgentRoot
    } catch { }

    # Remove lock file
    if (Test-Path $LockFile) {
        Remove-Item -Path $LockFile -Force -ErrorAction SilentlyContinue
    }

    # Release mutex
    if ($Mutex) {
        try {
            $Mutex.ReleaseMutex()
        } catch { }
        try {
            $Mutex.Dispose()
        } catch { }
    }

    Write-TiaAgentLog -Level 'INFO' -InstanceId $InstanceId -Event 'shutdown_complete' -Message "Supervisor shutdown complete"
}

try {
    # Step 2: Create directory structure
    Write-Host "[2/16] Initializing paths..." -ForegroundColor Yellow
    $tiaAgentRoot = Join-Path $env:LOCALAPPDATA 'TiaAgent'
    Initialize-TiaAgentPaths -TiaAgentRoot $tiaAgentRoot
    Write-Host "  Root: $tiaAgentRoot" -ForegroundColor Green

    # Step 3: Validate prerequisites (deferred until runtime is known — see step 5b)

    # Step 4: Clean stale runtime state
    Write-Host "[4/16] Checking for stale runtime..." -ForegroundColor Yellow
    Test-TiaAgentStaleRuntime -TiaAgentRoot $tiaAgentRoot -InstanceId $instanceId
    Write-Host "  Stale state cleaned" -ForegroundColor Green

    # Step 5: Load settings
    Write-Host "[5/16] Loading settings..." -ForegroundColor Yellow
    if ($Config) {
        $settings = Read-TiaAgentSettings -SettingsPath $Config
    } else {
        $settings = Read-TiaAgentSettings
    }
    Write-Host "  Startup timeout: $($settings.startupTimeoutSeconds)s" -ForegroundColor Green

    # Step 5b: Load runtime configuration
    $runtimeConfigPath = Join-Path $tiaAgentRoot 'config.json'
    $defaultRuntime = 'opencode'
    $runtimeConfig = $null
    $runtimeMode = 'cli'
    if (Test-Path $runtimeConfigPath) {
        try {
            $runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
            if ($runtimeConfig.defaultRuntime) {
                $defaultRuntime = $runtimeConfig.defaultRuntime
            }
            # Determine mode for the selected runtime
            $runtimeEntry = $null
            if ($runtimeConfig.runtimes -and $runtimeConfig.runtimes.$defaultRuntime) {
                $runtimeEntry = $runtimeConfig.runtimes.$defaultRuntime
            }
            if ($runtimeEntry -and $runtimeEntry.mode) {
                $runtimeMode = $runtimeEntry.mode
            } elseif ($defaultRuntime -eq 'opencode') {
                $runtimeMode = 'server'
            }
            Write-Host "  Runtime config loaded: default=$defaultRuntime, mode=$runtimeMode" -ForegroundColor Green
        }
        catch {
            Write-Host "  WARNING: Failed to parse runtime config: $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    } else {
        # No config file — opencode defaults to server mode
        if ($defaultRuntime -eq 'opencode') { $runtimeMode = 'server' }
        Write-Host "  No runtime config found, using default: $defaultRuntime ($runtimeMode)" -ForegroundColor Gray
    }

    $runtimeNeedsServer = ($runtimeMode -eq 'server')

    # Step 3 (deferred): Validate prerequisites for the selected runtime
    Write-Host "[3/16] Validating prerequisites..." -ForegroundColor Yellow
    $prereqs = Test-TiaAgentPrerequisites -TiaAgentRoot $tiaAgentRoot -RepoRoot $RepoRoot -RuntimeId $defaultRuntime
    if (-not $prereqs.IsValid) {
        Write-Host "  FAILED:" -ForegroundColor Red
        foreach ($err in $prereqs.Errors) {
            Write-Host "    - $err" -ForegroundColor Red
        }
        throw "Prerequisites validation failed"
    }
    if ($prereqs.Warnings.Count -gt 0) {
        foreach ($warn in $prereqs.Warnings) {
            Write-Host "  WARNING: $warn" -ForegroundColor DarkYellow
        }
    }
    Write-Host "  Prerequisites OK" -ForegroundColor Green

    # Step 6: Allocate ports
    Write-Host "[6/16] Allocating ports..." -ForegroundColor Yellow
    $bridgePort = Get-TiaAgentPort -ServiceName 'bridge' -PreferredPort $settings.preferredPorts.bridge -RangeStart $settings.portRange.start -RangeEnd $settings.portRange.end
    Write-Host "  Bridge: $bridgePort" -ForegroundColor Green

    $runtimePort = 0
    if ($runtimeNeedsServer) {
        $preferredRuntimePort = if ($settings.preferredPorts.opencode) { $settings.preferredPorts.opencode } else { 43120 }
        $runtimePort = Get-TiaAgentPort -ServiceName $defaultRuntime -PreferredPort $preferredRuntimePort -RangeStart $settings.portRange.start -RangeEnd $settings.portRange.end
        Write-Host "  Runtime server: $runtimePort" -ForegroundColor Green
    } else {
        Write-Host "  Runtime server: not needed (CLI mode)" -ForegroundColor Gray
    }

    # Step 7: Generate transient credentials
    Write-Host "[7/16] Generating credentials..." -ForegroundColor Yellow
    $secretsDir = Join-Path (Join-Path $tiaAgentRoot 'runtime') 'secrets'
    if (-not (Test-Path $secretsDir)) {
        New-Item -ItemType Directory -Path $secretsDir -Force | Out-Null
    }
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    $mcpToken = [Convert]::ToBase64String($bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')
    $mcpTokenFile = Join-Path $secretsDir 'mcp.token'
    $mcpToken | Out-File -FilePath $mcpTokenFile -Encoding UTF8 -Force
    Write-Host "  MCP token generated" -ForegroundColor Green

    # Step 8: Publish runtime status as starting
    Write-Host "[8/16] Publishing runtime manifest..." -ForegroundColor Yellow
    $runtimeDisplayName = switch ($defaultRuntime) {
        'mimo' { 'Mimo CLI' }
        'opencode' { 'OpenCode' }
        'claude' { 'Claude Code CLI' }
        default { $defaultRuntime }
    }
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'starting' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'starting' -OpenCodeStatus 'pending' -RuntimeId $defaultRuntime -RuntimeDisplayName $runtimeDisplayName -RuntimeMode $runtimeMode -RuntimeHealthy $false
    Write-Host "  Manifest published (status: starting, runtime: $defaultRuntime)" -ForegroundColor Green

    # Step 9: Start Bridge
    Write-Host "[9/16] Starting Bridge..." -ForegroundColor Yellow
    $bridgeDll = Join-Path $RepoRoot 'src\TiaAgent.Bridge\bin\Release\net8.0\TiaAgent.Bridge.dll'
    if (-not (Test-Path $bridgeDll)) {
        $bridgeDll = Join-Path $RepoRoot 'src\TiaAgent.Bridge\bin\Debug\net8.0\TiaAgent.Bridge.dll'
    }

    # Write bridge.json with allocated port
    $bridgeConfigPath = Join-Path $tiaAgentRoot 'bridge.json'
    $bridgeConfig = [ordered]@{
        port              = $bridgePort
        openCodeBaseUrl   = if ($runtimeNeedsServer) { "http://127.0.0.1:$runtimePort" } else { '' }
        taskTimeoutSeconds = 300
        maxConcurrentTasks = 5
        maxRequestBodyBytes = 1048576
    }
    $bridgeConfig | ConvertTo-Json | Out-File -FilePath $bridgeConfigPath -Encoding UTF8 -Force

    $bridgeLog = Join-Path (Join-Path $tiaAgentRoot 'logs') 'bridge.log'
    $bridgeResult = Start-TiaAgentService -ServiceName 'bridge' -ExecutablePath 'dotnet' -Arguments @('exec', $bridgeDll) -WorkingDirectory $RepoRoot -LogFile $bridgeLog -InstanceId $instanceId -EnvironmentVariables @{ 'TIA_AGENT_INSTANCE_ID' = $instanceId }
    $bridgeProcess = $bridgeResult.Process
    Write-Host "  Bridge started (PID: $($bridgeResult.Pid))" -ForegroundColor Green

    # Step 10: Wait for Bridge health
    Write-Host "[10/16] Waiting for Bridge health..." -ForegroundColor Yellow
    $bridgeHealthUrl = "http://127.0.0.1:$bridgePort/health"
    $bridgeHealthy = Wait-TiaAgentHealth -Url $bridgeHealthUrl -TimeoutSeconds $settings.healthCheckTimeoutSeconds -RetryIntervalMs $settings.healthCheckRetryIntervalMs -ServiceName 'bridge'
    if (-not $bridgeHealthy) {
        New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'failed' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'failed'
        throw "Bridge health check failed"
    }

    # Update manifest with bridge PID
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'starting' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'healthy' -OpenCodeStatus 'pending' -BridgePid $bridgeResult.Pid
    Write-Host "  Bridge healthy" -ForegroundColor Green

    # Step 11: Generate runtime server config (only for server-mode runtimes)
    if ($runtimeNeedsServer) {
        Write-Host "[11/16] Generating runtime server config ($defaultRuntime)..." -ForegroundColor Yellow
        $runtimeWorkDir = $null
        if ($defaultRuntime -eq 'opencode') {
            $mimoConfig = New-TiaAgentOpenCodeConfig -TiaAgentRoot $tiaAgentRoot -OpenCodePort $runtimePort
            $runtimeWorkDir = $mimoConfig.WorkingDirectory
            Write-Host "  Config: $($mimoConfig.ConfigPath)" -ForegroundColor Green
        } else {
            $runtimeWorkDir = Join-Path (Join-Path $tiaAgentRoot 'runtime') "$defaultRuntime-workdir"
            if (-not (Test-Path $runtimeWorkDir)) {
                New-Item -ItemType Directory -Path $runtimeWorkDir -Force | Out-Null
            }
            Write-Host "  Work dir: $runtimeWorkDir" -ForegroundColor Green
        }
    } else {
        Write-Host "[11/16] Skipping runtime config (CLI mode: $defaultRuntime)..." -ForegroundColor Gray
    }

    # Step 12: Start runtime server (only if selected runtime requires server mode)
    $runtimeExeResult = $null
    if ($runtimeNeedsServer) {
        Write-Host "[12/16] Starting runtime server ($defaultRuntime, mode=$runtimeMode)..." -ForegroundColor Yellow
        $runtimeLog = Join-Path (Join-Path $tiaAgentRoot 'logs') "$defaultRuntime.log"

        if ($defaultRuntime -eq 'opencode') {
            # OpenCode uses mimo serve
            $mimoExe = Get-Command mimo -ErrorAction SilentlyContinue
            if (-not $mimoExe) {
                throw "Runtime server executable (mimo) not found for '$defaultRuntime' in server mode"
            }

            $mimoSource = $mimoExe.Source
            $mimoDir = Split-Path $mimoSource -Parent
            $nodeExe = Join-Path $mimoDir 'node.exe'
            $mimoScript = Join-Path $mimoDir 'node_modules\@mimo-ai\cli\bin\mimo'

            if ((Test-Path $nodeExe) -and (Test-Path $mimoScript)) {
                $runtimeExeResult = Start-TiaAgentService -ServiceName $defaultRuntime -ExecutablePath $nodeExe -Arguments @($mimoScript, 'serve', '--port', $runtimePort.ToString()) -WorkingDirectory $runtimeWorkDir -LogFile $runtimeLog -InstanceId $instanceId
            } else {
                $runtimeExeResult = Start-TiaAgentService -ServiceName $defaultRuntime -ExecutablePath $mimoSource -Arguments @('serve', '--port', $runtimePort.ToString()) -WorkingDirectory $runtimeWorkDir -LogFile $runtimeLog -InstanceId $instanceId
            }
        } else {
            # Generic server-mode runtime
            $runtimeExe = Get-Command $defaultRuntime -ErrorAction SilentlyContinue
            if (-not $runtimeExe) {
                throw "Runtime executable '$defaultRuntime' not found for server mode"
            }
            $runtimeExeResult = Start-TiaAgentService -ServiceName $defaultRuntime -ExecutablePath $runtimeExe.Source -Arguments @('serve', '--port', $runtimePort.ToString()) -WorkingDirectory $runtimeWorkDir -LogFile $runtimeLog -InstanceId $instanceId
        }

        $runtimeProcess = $runtimeExeResult.Process
        Write-Host "  Runtime server started (PID: $($runtimeExeResult.Pid))" -ForegroundColor Green

        # Step 13: Wait for runtime server health
        Write-Host "[13/16] Waiting for runtime server health..." -ForegroundColor Yellow
        $runtimeHealthUrl = "http://127.0.0.1:$runtimePort/health"
        $runtimeHealthy = Wait-TiaAgentHealth -Url $runtimeHealthUrl -TimeoutSeconds $settings.healthCheckTimeoutSeconds -RetryIntervalMs $settings.healthCheckRetryIntervalMs -ServiceName $defaultRuntime
        if (-not $runtimeHealthy) {
            New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'failed' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'healthy' -OpenCodeStatus 'failed' -BridgePid $bridgeResult.Pid -OpenCodePid $runtimeExeResult.Pid -RuntimeId $defaultRuntime -RuntimeDisplayName $runtimeDisplayName -RuntimeMode $runtimeMode -RuntimeHealthy $false
            throw "Runtime server health check failed for '$defaultRuntime'"
        }
        Write-Host "  Runtime server healthy" -ForegroundColor Green
    } else {
        Write-Host "[12/16] Skipping runtime server (CLI mode: $defaultRuntime)..." -ForegroundColor Gray
        Write-Host "[13/16] Skipping server health check (CLI mode)..." -ForegroundColor Gray
        # In CLI mode, verify the runtime executable is available
        $runtimeExeCheck = Get-Command $defaultRuntime -ErrorAction SilentlyContinue
        if (-not $runtimeExeCheck) {
            New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'failed' -BridgePort $bridgePort -OpenCodePort 0 -BridgeStatus 'healthy' -OpenCodeStatus 'skipped' -BridgePid $bridgeResult.Pid -RuntimeId $defaultRuntime -RuntimeDisplayName $runtimeDisplayName -RuntimeMode $runtimeMode -RuntimeHealthy $false
            throw "Runtime executable '$defaultRuntime' not found for CLI mode"
        }
        Write-Host "  Runtime executable found: $($runtimeExeCheck.Source)" -ForegroundColor Green
    }

    # Step 14: Publish runtime status as ready
    Write-Host "[14/16] Publishing ready status..." -ForegroundColor Yellow
    $runtimeStatusFinal = if ($runtimeNeedsServer) { 'healthy' } else { 'skipped' }
    $runtimePidFinal = if ($runtimeExeResult) { $runtimeExeResult.Pid } else { 0 }
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'ready' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'healthy' -OpenCodeStatus $runtimeStatusFinal -BridgePid $bridgeResult.Pid -OpenCodePid $runtimePidFinal -RuntimeId $defaultRuntime -RuntimeDisplayName $runtimeDisplayName -RuntimeMode $runtimeMode -RuntimeHealthy $true
    Write-Host "  Runtime status: ready" -ForegroundColor Green

    # Step 15: Display summary
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "  Runtime Ready" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Instance   : $instanceId" -ForegroundColor White
    Write-Host "Status     : Ready" -ForegroundColor Green
    Write-Host "Supervisor : Running, PID $PID" -ForegroundColor White
    Write-Host "Bridge     : Healthy, http://127.0.0.1:$bridgePort" -ForegroundColor White
    Write-Host "Runtime    : $defaultRuntime ($runtimeDisplayName, mode=$runtimeMode)" -ForegroundColor White
    if ($runtimeNeedsServer) {
        Write-Host "Server     : Healthy, http://127.0.0.1:$runtimePort" -ForegroundColor White
    } else {
        Write-Host "Server     : Not needed (CLI mode)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Runtime manifest: $tiaAgentRoot\runtime\runtime.json" -ForegroundColor Gray
    Write-Host "Runtime config:   $runtimeConfigPath" -ForegroundColor Gray
    Write-Host ""

    # Step 16: Monitor child processes
    if ($NoMonitor) {
        Write-Host "Exiting (NoMonitor mode)..." -ForegroundColor Yellow
    }
    else {
        Write-Host "Monitoring services (Ctrl+C to stop)..." -ForegroundColor Gray
        Write-Host ""

        # Set up Ctrl+C handler (may fail if no console is attached)
        $hasConsole = $false
        try {
            [Console]::TreatControlCAsInput = $true
            $hasConsole = $true
        } catch {
            Write-Host "  No console attached, Ctrl+C handler disabled" -ForegroundColor DarkYellow
        }

        while (-not $shutdownRequested) {
            # Check for Ctrl+C (only if console is available)
            if ($hasConsole -and [Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Modifiers -band [ConsoleModifiers]::Control -and $key.Key -eq 'C') {
                    $shutdownRequested = $true
                    Write-Host ""
                    Write-Host "Shutdown requested..." -ForegroundColor Yellow
                    break
                }
            }

            # Check process health
            if ($bridgeProcess -and $bridgeProcess.HasExited) {
                Write-TiaAgentLog -Level 'ERROR' -InstanceId $instanceId -Event 'bridge_exited' -Message "Bridge exited unexpectedly with code $($bridgeProcess.ExitCode)"
                New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'degraded' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'failed' -OpenCodeStatus $(if ($runtimeNeedsServer) { 'healthy' } else { 'skipped' }) -RuntimeId $defaultRuntime -RuntimeDisplayName $runtimeDisplayName -RuntimeMode $runtimeMode -RuntimeHealthy $true
                Write-Host "  WARNING: Bridge exited (code: $($bridgeProcess.ExitCode))" -ForegroundColor Red
            }

            if ($runtimeProcess -and $runtimeProcess.HasExited) {
                Write-TiaAgentLog -Level 'ERROR' -InstanceId $instanceId -Event 'runtime_exited' -Message "Runtime server exited unexpectedly with code $($runtimeProcess.ExitCode)"
                New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'degraded' -BridgePort $bridgePort -OpenCodePort $runtimePort -BridgeStatus 'healthy' -OpenCodeStatus 'failed' -RuntimeId $defaultRuntime -RuntimeDisplayName $runtimeDisplayName -RuntimeMode $runtimeMode -RuntimeHealthy $false
                Write-Host "  WARNING: Runtime server exited (code: $($runtimeProcess.ExitCode))" -ForegroundColor Red
            }

            Start-Sleep -Milliseconds 1000
        }
    }
}
catch {
    Write-Host ""
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-TiaAgentLog -Level 'ERROR' -InstanceId $instanceId -Event 'startup_error' -Message "Startup failed: $($_.Exception.Message)"

    # Update manifest to failed state
    try {
        New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'failed'
    } catch { }

    exit 1
}
finally {
    # Run cleanup
    & $cleanup -InstanceId $instanceId -TiaAgentRoot $tiaAgentRoot -Mutex $lock.Mutex -LockFile $lock.LockFilePath -BridgeProcess $bridgeProcess -RuntimeProcess $runtimeProcess
}
