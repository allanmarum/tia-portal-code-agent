function Test-TiaAgentStaleRuntime {
    <#
    .SYNOPSIS
        Detects and cleans stale runtime state from a previous supervisor run.
    .PARAMETER TiaAgentRoot
        TiaAgent root directory
    .PARAMETER InstanceId
        Current instance ID (to avoid cleaning own state)
    #>
    [CmdletBinding()]
    param(
        [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),
        [string]$InstanceId = ''
    )

    $runtimeDir = Join-Path $TiaAgentRoot 'runtime'
    $manifestPath = Join-Path $runtimeDir 'runtime.json'
    $lockPath = Join-Path $runtimeDir 'supervisor.lock'

    # Check for stale manifest
    if (Test-Path $manifestPath) {
        try {
            $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

            # Skip if it's our own manifest
            if ($manifest.instanceId -eq $InstanceId) {
                return
            }

            # Check if supervisor PID is still running
            $supervisorPid = $manifest.supervisorPid
            if ($supervisorPid -gt 0) {
                $process = Get-Process -Id $supervisorPid -ErrorAction SilentlyContinue
                if ($process) {
                    Write-TiaAgentLog -Level 'INFO' -Event 'runtime_active' -Message "Previous runtime still active (PID: $supervisorPid). Skipping cleanup."
                    return
                }
            }

            Write-TiaAgentLog -Level 'WARN' -Event 'stale_runtime_detected' -Message "Stale runtime detected (instance: $($manifest.instanceId), PID: $supervisorPid). Cleaning up."
        }
        catch {
            Write-TiaAgentLog -Level 'WARN' -Event 'manifest_read_error' -Message "Cannot read stale manifest: $($_.Exception.Message)"
        }

        # Clean stale manifest
        Remove-Item -Path $manifestPath -Force -ErrorAction SilentlyContinue
        Write-TiaAgentLog -Level 'INFO' -Event 'stale_manifest_cleaned' -Message "Removed stale runtime.json"
    }

    # Check for stale lock file
    if (Test-Path $lockPath) {
        try {
            $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
            $lockPid = $lock.supervisorPid
            if ($lockPid -gt 0) {
                $process = Get-Process -Id $lockPid -ErrorAction SilentlyContinue
                if (-not $process) {
                    Remove-Item -Path $lockPath -Force -ErrorAction SilentlyContinue
                    Write-TiaAgentLog -Level 'INFO' -Event 'stale_lock_cleaned' -Message "Removed stale supervisor.lock"
                }
            }
        }
        catch {
            Remove-Item -Path $lockPath -Force -ErrorAction SilentlyContinue
            Write-TiaAgentLog -Level 'INFO' -Event 'stale_lock_cleaned' -Message "Removed corrupt supervisor.lock"
        }
    }

    # Clean stale secrets (older than 24 hours)
    $secretsDir = Join-Path $runtimeDir 'secrets'
    if (Test-Path $secretsDir) {
        Get-ChildItem -Path $secretsDir -File -ErrorAction SilentlyContinue | ForEach-Object {
            if ((Get-Date) - $_.LastWriteTime -gt [TimeSpan]::FromHours(24)) {
                Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
                Write-TiaAgentLog -Level 'INFO' -Event 'stale_secret_cleaned' -Message "Removed stale secret: $($_.Name)"
            }
        }
    }

    # Clean stale temp files (older than 1 hour)
    $tempDir = Join-Path $TiaAgentRoot 'temp'
    if (Test-Path $tempDir) {
        Get-ChildItem -Path $tempDir -File -ErrorAction SilentlyContinue | ForEach-Object {
            if ((Get-Date) - $_.LastWriteTime -gt [TimeSpan]::FromHours(1)) {
                Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
