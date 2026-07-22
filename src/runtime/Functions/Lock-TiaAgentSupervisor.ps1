function Lock-TiaAgentSupervisor {
    <#
    .SYNOPSIS
        Acquires single-instance protection using a named mutex and lock file.
    .PARAMETER TiaAgentRoot
        TiaAgent root directory
    .OUTPUTS
        PSCustomObject with Mutex, InstanceId, LockFilePath properties.
    #>
    [CmdletBinding()]
    param(
        [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent')
    )

    $mutexName = 'Local\TiaAgent.Supervisor'
    $mutex = $null
    $createdNew = $false

    try {
        $mutex = [System.Threading.Mutex]::new($false, $mutexName, [ref]$createdNew)
    }
    catch {
        throw "Failed to create supervisor mutex: $($_.Exception.Message)"
    }

    if (-not $createdNew) {
        # Another supervisor is running - check if stale
        $lockPath = Join-Path (Join-Path $TiaAgentRoot 'runtime') 'supervisor.lock'
        if (Test-Path $lockPath) {
            try {
                $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
                $existingPid = $lock.supervisorPid
                $existingProcess = Get-Process -Id $existingPid -ErrorAction SilentlyContinue
                if ($existingProcess) {
                    $isSupervisorProcess = ($existingProcess.ProcessName -like '*powershell*' -or $existingProcess.ProcessName -like '*pwsh*' -or $existingProcess.ProcessName -like '*TiaAgent*')
                    if ($isSupervisorProcess) {
                        throw "Another TIA Agent supervisor is already running (PID: $existingPid)"
                    }
                    else {
                        Write-TiaAgentLog -Level 'WARN' -Event 'stale_lock_pid_reused' -Message "PID $existingPid is in use by non-supervisor process '$($existingProcess.ProcessName)'. Cleaning stale lock."
                        Remove-Item -Path $lockPath -Force -ErrorAction SilentlyContinue
                        $mutex.Dispose()
                        $mutex = [System.Threading.Mutex]::new($false, $mutexName, [ref]$createdNew)
                        if (-not $createdNew) {
                            throw "Cannot acquire supervisor mutex after PID reuse cleanup"
                        }
                    }
                }
                else {
                    Write-TiaAgentLog -Level 'WARN' -Event 'stale_lock_detected' -Message "Stale lock detected for PID $existingPid. Cleaning up."
                    Remove-Item -Path $lockPath -Force -ErrorAction SilentlyContinue
                    # Try to acquire again
                    $mutex.Dispose()
                    $mutex = [System.Threading.Mutex]::new($false, $mutexName, [ref]$createdNew)
                    if (-not $createdNew) {
                        throw "Cannot acquire supervisor mutex after stale cleanup"
                    }
                }
            }
            catch {
                if ($_.Exception.Message -like '*already running*') { throw }
                Write-TiaAgentLog -Level 'WARN' -Event 'lock_parse_error' -Message "Cannot parse lock file: $($_.Exception.Message). Attempting cleanup."
                Remove-Item -Path $lockPath -Force -ErrorAction SilentlyContinue
                $mutex.Dispose()
                $mutex = [System.Threading.Mutex]::new($false, $mutexName, [ref]$createdNew)
                if (-not $createdNew) {
                    throw "Cannot acquire supervisor mutex after cleanup"
                }
            }
        }
    }

    # Generate instance ID
    $instanceId = (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + (Get-Random -Minimum 1000 -Maximum 9999).ToString()

    # Write lock file
    $lockDir = Join-Path $TiaAgentRoot 'runtime'
    if (-not (Test-Path $lockDir)) {
        New-Item -ItemType Directory -Path $lockDir -Force | Out-Null
    }

    $lockPath = Join-Path $lockDir 'supervisor.lock'
    $lockData = [ordered]@{
        instanceId   = $instanceId
        supervisorPid = $PID
        startedAt    = (Get-Date).ToString('o')
    }
    $lockData | ConvertTo-Json | Out-File -FilePath $lockPath -Encoding UTF8 -Force

    Write-TiaAgentLog -Level 'INFO' -InstanceId $instanceId -Event 'mutex_acquired' -Message "Supervisor mutex acquired. Instance: $instanceId"

    return [PSCustomObject]@{
        Mutex        = $mutex
        InstanceId   = $instanceId
        LockFilePath = $lockPath
    }
}
