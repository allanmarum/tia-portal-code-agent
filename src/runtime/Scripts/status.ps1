#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Shows runtime status and health information.

.DESCRIPTION
    Reads the runtime manifest, validates processes, and checks health endpoints.
    Supports both human-readable and JSON output formats.

.PARAMETER TiaAgentRoot
    TiaAgent root directory (defaults to %LOCALAPPDATA%\TiaAgent)

.PARAMETER Json
    Output in machine-readable JSON format

.EXAMPLE
    .\status.ps1
    .\status.ps1 -Json
#>
[CmdletBinding()]
param(
    [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),
    [switch]$Json
)

$ErrorActionPreference = 'SilentlyContinue'
$ScriptRoot = $PSScriptRoot
$ModuleRoot = Split-Path $ScriptRoot -Parent

# Import module
Import-Module (Join-Path $ModuleRoot 'TiaAgent.Supervisor.psd1') -Force

$runtimeDir = Join-Path $TiaAgentRoot 'runtime'
$manifestPath = Join-Path $runtimeDir 'runtime.json'
$lockPath = Join-Path $runtimeDir 'supervisor.lock'

# Read manifest
$manifest = $null
if (Test-Path $manifestPath) {
    try {
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    }
    catch {
        if ($Json) {
            @{ error = "Failed to parse runtime manifest: $($_.Exception.Message)" } | ConvertTo-Json
        }
        else {
            Write-Host "Failed to parse runtime manifest: $($_.Exception.Message)" -ForegroundColor Red
        }
        exit 1
    }
}

# Validate processes
$supervisorRunning = $false
$bridgeRunning = $false
$bridgeHealthy = $false
$opencodeRunning = $false
$opencodeHealthy = $false

if ($manifest) {
    # Check supervisor
    if ($manifest.supervisorPid -gt 0) {
        $supervisorProcess = Get-Process -Id $manifest.supervisorPid -ErrorAction SilentlyContinue
        $supervisorRunning = $null -ne $supervisorProcess
    }

    # Check bridge
    if ($manifest.services.bridge -and $manifest.services.bridge.pid -gt 0) {
        $bridgeProcess = Get-Process -Id $manifest.services.bridge.pid -ErrorAction SilentlyContinue
        $bridgeRunning = $null -ne $bridgeProcess

        if ($bridgeRunning -and $manifest.services.bridge.healthUrl) {
            try {
                $health = Invoke-RestMethod -Uri $manifest.services.bridge.healthUrl -TimeoutSec 3 -Method Get -ErrorAction Stop
                $bridgeHealthy = $health.status -eq 'ok' -or $health.status -eq 'healthy'
            }
            catch { }
        }
    }

    # Check opencode
    if ($manifest.services.opencode -and $manifest.services.opencode.pid -gt 0) {
        $opencodeProcess = Get-Process -Id $manifest.services.opencode.pid -ErrorAction SilentlyContinue
        $opencodeRunning = $null -ne $opencodeProcess

        if ($opencodeRunning -and $manifest.services.opencode.healthUrl) {
            try {
                $health = Invoke-RestMethod -Uri $manifest.services.opencode.healthUrl -TimeoutSec 3 -Method Get -ErrorAction Stop
                $opencodeHealthy = $health.status -eq 'healthy' -or $health.status -eq 'ok'
            }
            catch {
                # For non-2xx responses (like 503), check TCP connectivity as fallback.
                # mimo serve returns 503 for /health when Web UI is unavailable in headless mode.
                if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ge 500) {
                    try {
                        $tcp = New-Object System.Net.Sockets.TcpClient
                        $tcp.Connect("127.0.0.1", $manifest.services.opencode.port)
                        $tcp.Close()
                        $opencodeHealthy = $true
                    } catch { }
                }
            }
        }
    }
}

# Build status object
$status = [ordered]@{
    instanceId    = if ($manifest) { $manifest.instanceId } else { '' }
    status        = if ($manifest) { $manifest.status } else { 'unknown' }
    supervisor    = [ordered]@{
        running = $supervisorRunning
        pid     = if ($manifest) { $manifest.supervisorPid } else { 0 }
    }
    bridge        = [ordered]@{
        running = $bridgeRunning
        healthy = $bridgeHealthy
        pid     = if ($manifest -and $manifest.services.bridge) { $manifest.services.bridge.pid } else { 0 }
        host    = if ($manifest -and $manifest.services.bridge) { $manifest.services.bridge.host } else { '' }
        port    = if ($manifest -and $manifest.services.bridge) { $manifest.services.bridge.port } else { 0 }
        url     = if ($manifest -and $manifest.services.bridge) { $manifest.services.bridge.baseUrl } else { '' }
    }
    opencode      = [ordered]@{
        running = $opencodeRunning
        healthy = $opencodeHealthy
        pid     = if ($manifest -and $manifest.services.opencode) { $manifest.services.opencode.pid } else { 0 }
        host    = if ($manifest -and $manifest.services.opencode) { $manifest.services.opencode.host } else { '' }
        port    = if ($manifest -and $manifest.services.opencode) { $manifest.services.opencode.port } else { 0 }
        url     = if ($manifest -and $manifest.services.opencode) { $manifest.services.opencode.baseUrl } else { '' }
    }
    runtimePath   = $manifestPath
}

if ($Json) {
    $status | ConvertTo-Json -Depth 10
}
else {
    Write-Host ""
    Write-Host "TIA Agent Runtime" -ForegroundColor Cyan
    Write-Host ""

    $instanceShort = if ($status.instanceId) { $status.instanceId.Substring(0, [Math]::Min(8, $status.instanceId.Length)) } else { 'N/A' }
    Write-Host ("Instance   : {0}" -f $instanceShort) -ForegroundColor White

    $statusColor = switch ($status.status) {
        'ready'    { 'Green' }
        'starting' { 'Yellow' }
        'stopping' { 'Yellow' }
        'stopped'  { 'Gray' }
        'failed'   { 'Red' }
        'degraded' { 'DarkYellow' }
        default    { 'White' }
    }
    Write-Host ("Status     : {0}" -f $status.status) -ForegroundColor $statusColor

    $supervisorText = if ($status.supervisor.running) { "Running, PID $($status.supervisor.pid)" } else { "Not running" }
    $supervisorColor = if ($status.supervisor.running) { 'Green' } else { 'Red' }
    Write-Host ("Supervisor : {0}" -f $supervisorText) -ForegroundColor $supervisorColor

    $bridgeText = if ($status.bridge.running -and $status.bridge.healthy) {
        "Healthy, $($status.bridge.url)"
    } elseif ($status.bridge.running) {
        "Running (not healthy), PID $($status.bridge.pid)"
    } else {
        "Not running"
    }
    $bridgeColor = if ($status.bridge.running -and $status.bridge.healthy) { 'Green' } elseif ($status.bridge.running) { 'Yellow' } else { 'Red' }
    Write-Host ("Bridge     : {0}" -f $bridgeText) -ForegroundColor $bridgeColor

    $opencodeText = if ($status.opencode.running -and $status.opencode.healthy) {
        "Healthy, $($status.opencode.url)"
    } elseif ($status.opencode.running) {
        "Running (not healthy), PID $($status.opencode.pid)"
    } else {
        "Not running"
    }
    $opencodeColor = if ($status.opencode.running -and $status.opencode.healthy) { 'Green' } elseif ($status.opencode.running) { 'Yellow' } else { 'Red' }
    Write-Host ("Runtime    : {0}" -f $opencodeText) -ForegroundColor $opencodeColor

    # Show runtime identity if available
    if ($manifest -and $manifest.runtime) {
        $rt = $manifest.runtime
        $rtName = if ($rt.displayName) { $rt.displayName } else { $rt.id }
        $rtMode = if ($rt.mode) { $rt.mode } else { 'unknown' }
        $rtHealthy = if ($rt.healthy) { 'yes' } else { 'no' }
        Write-Host ("           : {0} (mode={1}, healthy={2})" -f $rtName, $rtMode, $rtHealthy) -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "Runtime manifest:" -ForegroundColor Cyan
    Write-Host $status.runtimePath -ForegroundColor Gray
    Write-Host ""
}
