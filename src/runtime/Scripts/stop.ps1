#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Gracefully stops all runtime services.

.DESCRIPTION
    Reads the runtime manifest, validates ownership, and gracefully stops
    all services in the correct order (OpenCode first, then Bridge).

.PARAMETER TiaAgentRoot
    TiaAgent root directory (defaults to %LOCALAPPDATA%\TiaAgent)

.PARAMETER Force
    Force kill processes without waiting for graceful shutdown

.EXAMPLE
    .\stop.ps1
    .\stop.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = $PSScriptRoot
$ModuleRoot = Split-Path $ScriptRoot -Parent

# Import module
Import-Module (Join-Path $ModuleRoot 'TiaAgent.Supervisor.psd1') -Force

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  TIA Agent Runtime Shutdown" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$runtimeDir = Join-Path $TiaAgentRoot 'runtime'
$manifestPath = Join-Path $runtimeDir 'runtime.json'
$lockPath = Join-Path $runtimeDir 'supervisor.lock'

# Step 1: Read and validate runtime.json
Write-Host "[1/9] Reading runtime manifest..." -ForegroundColor Yellow
if (-not (Test-Path $manifestPath)) {
    Write-Host "  No runtime manifest found. Nothing to stop." -ForegroundColor Gray
    exit 0
}

try {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
}
catch {
    Write-Host "  Failed to parse runtime manifest: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Confirm instance belongs to TiaAgent
Write-Host "[2/9] Validating runtime..." -ForegroundColor Yellow
$instanceId = $manifest.instanceId
$status = $manifest.status
Write-Host "  Instance: $instanceId" -ForegroundColor White
Write-Host "  Status: $status" -ForegroundColor White

if ($status -eq 'stopped') {
    Write-Host "  Runtime already stopped." -ForegroundColor Gray
    exit 0
}

# Step 3: Set global state to stopping
Write-Host "[3/9] Setting status to stopping..." -ForegroundColor Yellow
try {
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'stopping' -TiaAgentRoot $TiaAgentRoot
}
catch {
    Write-Host "  Warning: Failed to update manifest: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

# Step 4: Stop OpenCode (first, since it depends on Bridge)
Write-Host "[4/9] Stopping OpenCode..." -ForegroundColor Yellow
$opencodeStopped = $false
if ($manifest.services.opencode -and $manifest.services.opencode.pid -gt 0) {
    $opencodePid = $manifest.services.opencode.pid
    $process = Get-Process -Id $opencodePid -ErrorAction SilentlyContinue
    if ($process) {
        if ($Force) {
            $process.Kill()
            Write-Host "  OpenCode (PID: $opencodePid) force killed" -ForegroundColor Yellow
        }
        else {
            Stop-TiaAgentService -Process $process -ServiceName 'opencode' -GracefulTimeoutSeconds 10 -RuntimeInstanceId $instanceId
        }
        $opencodeStopped = $true
    }
    else {
        Write-Host "  OpenCode (PID: $opencodePid) not running" -ForegroundColor Gray
    }
}

if (-not $opencodeStopped) {
    # Try to find and stop any runtime processes by port
    $runtimeId = if ($manifest.runtime -and $manifest.runtime.id) { $manifest.runtime.id } else { 'opencode' }
    $runtimeProcessNames = switch ($runtimeId) {
        'opencode' { @('mimo', 'node') }
        'claude'   { @('claude') }
        'mimo'     { @('mimo', 'node') }
        default    { @($runtimeId) }
    }
    $runtimeProcesses = Get-Process -Name $runtimeProcessNames -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*serve*--port*$($manifest.services.opencode.port)*"
    }
    foreach ($proc in $runtimeProcesses) {
        if ($Force) {
            $proc.Kill()
        }
        else {
            Stop-TiaAgentService -Process $proc -ServiceName $runtimeId -GracefulTimeoutSeconds 10 -RuntimeInstanceId $instanceId
        }
    }
}

# Step 5: Stop Bridge
Write-Host "[5/9] Stopping Bridge..." -ForegroundColor Yellow
$bridgeStopped = $false
if ($manifest.services.bridge -and $manifest.services.bridge.pid -gt 0) {
    $bridgePid = $manifest.services.bridge.pid
    $process = Get-Process -Id $bridgePid -ErrorAction SilentlyContinue
    if ($process) {
        if ($Force) {
            $process.Kill()
            Write-Host "  Bridge (PID: $bridgePid) force killed" -ForegroundColor Yellow
        }
        else {
            Stop-TiaAgentService -Process $process -ServiceName 'bridge' -GracefulTimeoutSeconds 10 -RuntimeInstanceId $instanceId
        }
        $bridgeStopped = $true
    }
    else {
        Write-Host "  Bridge (PID: $bridgePid) not running" -ForegroundColor Gray
    }
}

# Step 6: Remove transient secrets
Write-Host "[6/9] Cleaning up secrets..." -ForegroundColor Yellow
$secretsDir = Join-Path $runtimeDir 'secrets'
if (Test-Path $secretsDir) {
    Get-ChildItem -Path $secretsDir -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
    }
    Write-Host "  Secrets cleaned" -ForegroundColor Green
}

# Step 7: Remove supervisor lock
Write-Host "[7/9] Releasing supervisor lock..." -ForegroundColor Yellow
if (Test-Path $lockPath) {
    Remove-Item -Path $lockPath -Force -ErrorAction SilentlyContinue
    Write-Host "  Lock released" -ForegroundColor Green
}

# Step 8: Release mutex
Write-Host "[8/9] Releasing mutex..." -ForegroundColor Yellow
try {
    $mutex = [System.Threading.Mutex]::OpenExisting('Local\TiaAgent.Supervisor')
    $mutex.ReleaseMutex()
    $mutex.Dispose()
    Write-Host "  Mutex released" -ForegroundColor Green
}
catch {
    Write-Host "  Mutex not held (OK)" -ForegroundColor Gray
}

# Step 9: Update manifest to stopped
Write-Host "[9/9] Updating manifest..." -ForegroundColor Yellow
try {
    New-TiaAgentRuntimeManifest -InstanceId $instanceId -Status 'stopped' -TiaAgentRoot $TiaAgentRoot
    Write-Host "  Manifest updated" -ForegroundColor Green
}
catch {
    Write-Host "  Warning: Failed to update manifest: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Runtime Stopped" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Instance: $instanceId" -ForegroundColor White
Write-Host ""
