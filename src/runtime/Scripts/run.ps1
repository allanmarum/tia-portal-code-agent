#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Starts and manages all runtime services.

.DESCRIPTION
    Idempotent bootstrap and supervisor for TIA Portal Code Agent services.
    Starts Bridge and OpenCode, manages ports, publishes runtime manifest,
    and monitors child processes.

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
$opencodeProcess = $null
$shutdownRequested = $false

$cleanup = {
    param($InstanceId, $TiaAgentRoot, $Mutex, $LockFile, $BridgeProcess, $OpenCodeProcess)

    Write-TiaAgentLog -Level 'INFO' -InstanceId $InstanceId -Event 'shutdown_start' -Message "Supervisor shutting down..."

    # Stop services
    if ($BridgeProcess -and -not $BridgeProcess.HasExited) {
        Stop-TiaAgentService -Process $BridgeProcess -ServiceName 'bridge' -RuntimeInstanceId $InstanceId
    }
    if ($OpenCodeProcess -and -not $OpenCodeProcess.HasExited) {
        Stop-TiaAgentService -Process $OpenCodeProcess -ServiceName 'opencode' -RuntimeInstanceId $InstanceId
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

    # Step 3: Validate prerequisites
    Write-Host "[3/16] Validating prerequisites..." -ForegroundColor Yellow
    $prereqs = Test-TiaAgentPrerequisites -TiaAgentRoot $tiaAgentRoot -RepoRoot $RepoRoot
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

    # Step 6: Allocate ports
    Write-Host "[6/16] Allocating ports..." -ForegroundColor Yellow
    $bridgePort = Get-TiaAgentPort -ServiceName 'bridge' -PreferredPort $settings.preferredPorts.bridge -RangeStart $settings.portRange.start -RangeEnd $settings.portRange.end
    $opencodePort = Get-TiaAgentPort -ServiceName 'opencode' -PreferredPort $settings.preferredPorts.opencode -RangeStart $settings.portRange.start -RangeEnd $settings.portRange.end
    Write-Host "  Bridge: $bridgePort" -ForegroundColor Green
    Write-Host "  OpenCode: $opencodePort" -ForegroundColor Green

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
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'starting' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'starting' -OpenCodeStatus 'pending'
    Write-Host "  Manifest published (status: starting)" -ForegroundColor Green

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
        openCodeBaseUrl   = "http://127.0.0.1:$opencodePort"
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
        New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'failed' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'failed'
        throw "Bridge health check failed"
    }

    # Update manifest with bridge PID
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'starting' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'healthy' -OpenCodeStatus 'pending' -BridgePid $bridgeResult.Pid
    Write-Host "  Bridge healthy" -ForegroundColor Green

    # Step 11: Generate OpenCode config
    Write-Host "[11/16] Generating OpenCode config..." -ForegroundColor Yellow
    $mimoConfig = New-TiaAgentOpenCodeConfig -TiaAgentRoot $tiaAgentRoot -OpenCodePort $opencodePort
    Write-Host "  Config: $($mimoConfig.ConfigPath)" -ForegroundColor Green

    # Step 12: Start OpenCode
    Write-Host "[12/16] Starting OpenCode..." -ForegroundColor Yellow
    $opencodeLog = Join-Path (Join-Path $tiaAgentRoot 'logs') 'opencode.log'

    # Find mimo executable - resolve to actual node.exe for proper process tracking.
    # The mimo.ps1 wrapper spawns a child node.exe process that survives if we
    # only track the wrapper. By running node.exe directly, we track the real process.
    $mimoExe = Get-Command mimo -ErrorAction SilentlyContinue
    if (-not $mimoExe) {
        throw "OpenCode CLI (mimo) not found"
    }

    $mimoSource = $mimoExe.Source
    $mimoDir = Split-Path $mimoSource -Parent
    $nodeExe = Join-Path $mimoDir 'node.exe'
    $mimoScript = Join-Path $mimoDir 'node_modules\@mimo-ai\cli\bin\mimo'

    if ((Test-Path $nodeExe) -and (Test-Path $mimoScript)) {
        # Use node.exe directly for proper process lifecycle tracking
        $opencodeResult = Start-TiaAgentService -ServiceName 'opencode' -ExecutablePath $nodeExe -Arguments @($mimoScript, 'serve', '--port', $opencodePort.ToString()) -WorkingDirectory $mimoConfig.WorkingDirectory -LogFile $opencodeLog -InstanceId $instanceId
    } else {
        # Fallback to mimo wrapper
        $opencodeResult = Start-TiaAgentService -ServiceName 'opencode' -ExecutablePath $mimoSource -Arguments @('serve', '--port', $opencodePort.ToString()) -WorkingDirectory $mimoConfig.WorkingDirectory -LogFile $opencodeLog -InstanceId $instanceId
    }
    $opencodeProcess = $opencodeResult.Process
    Write-Host "  OpenCode started (PID: $($opencodeResult.Pid))" -ForegroundColor Green

    # Step 13: Wait for OpenCode health
    Write-Host "[13/16] Waiting for OpenCode health..." -ForegroundColor Yellow
    $opencodeHealthUrl = "http://127.0.0.1:$opencodePort/health"
    $opencodeHealthy = Wait-TiaAgentHealth -Url $opencodeHealthUrl -TimeoutSeconds $settings.healthCheckTimeoutSeconds -RetryIntervalMs $settings.healthCheckRetryIntervalMs -ServiceName 'opencode'
    if (-not $opencodeHealthy) {
        New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'failed' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'healthy' -OpenCodeStatus 'failed' -BridgePid $bridgeResult.Pid -OpenCodePid $opencodeResult.Pid
        throw "OpenCode health check failed"
    }
    Write-Host "  OpenCode healthy" -ForegroundColor Green

    # Step 14: Publish runtime status as ready
    Write-Host "[14/16] Publishing ready status..." -ForegroundColor Yellow
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'ready' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'healthy' -OpenCodeStatus 'healthy' -BridgePid $bridgeResult.Pid -OpenCodePid $opencodeResult.Pid
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
    Write-Host "OpenCode   : Healthy, http://127.0.0.1:$opencodePort" -ForegroundColor White
    Write-Host ""
    Write-Host "Runtime: $tiaAgentRoot\runtime\runtime.json" -ForegroundColor Gray
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
                New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'degraded' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'failed' -OpenCodeStatus 'healthy'
                Write-Host "  WARNING: Bridge exited (code: $($bridgeProcess.ExitCode))" -ForegroundColor Red
            }

            if ($opencodeProcess -and $opencodeProcess.HasExited) {
                Write-TiaAgentLog -Level 'ERROR' -InstanceId $instanceId -Event 'opencode_exited' -Message "OpenCode exited unexpectedly with code $($opencodeProcess.ExitCode)"
                New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'degraded' -BridgePort $bridgePort -OpenCodePort $opencodePort -BridgeStatus 'healthy' -OpenCodeStatus 'failed'
                Write-Host "  WARNING: OpenCode exited (code: $($opencodeProcess.ExitCode))" -ForegroundColor Red
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
    & $cleanup -InstanceId $instanceId -TiaAgentRoot $tiaAgentRoot -Mutex $lock.Mutex -LockFile $lock.LockFilePath -BridgeProcess $bridgeProcess -OpenCodeProcess $opencodeProcess
}
