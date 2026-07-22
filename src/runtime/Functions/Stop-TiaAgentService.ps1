function Stop-TiaAgentService {
    <#
    .SYNOPSIS
        Gracefully stops a service process with bounded timeout.
    .PARAMETER Process
        The process to stop
    .PARAMETER ServiceName
        Service name for logging
    .PARAMETER GracefulTimeoutSeconds
        Time to wait for graceful shutdown before force-killing
    .PARAMETER RuntimeInstanceId
        Runtime instance ID to validate process ownership
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [string]$ServiceName,

        [int]$GracefulTimeoutSeconds = 10,

        [string]$RuntimeInstanceId = ''
    )

    $pid = $Process.Id

    # Validate process is still running
    if ($Process.HasExited) {
        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'process_already_stopped' -Message "$ServiceName (PID: $pid) already stopped"
        return
    }

    # Validate process identity (PID and start time/identity matching)
    try {
        $currentProcess = Get-Process -Id $pid -ErrorAction Stop
        
        # Verify process has not been reused by OS for an unrelated process
        if ($Process.StartTime -and $currentProcess.StartTime -and $Process.StartTime -ne $currentProcess.StartTime) {
            Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'process_pid_reused' -Message "PID $pid was reused by process '$($currentProcess.ProcessName)' (started $($currentProcess.StartTime)). Skipping stop."
            return
        }

        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'process_stop_requested' -Message "Requesting graceful shutdown of $ServiceName (PID: $pid, name: $($currentProcess.ProcessName))"
    }
    catch {
        Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'process_not_found' -Message "$ServiceName (PID: $pid) not found. Process may have already exited."
        return
    }

    # Attempt graceful shutdown
    try {
        $Process.CloseMainWindow() | Out-Null
        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'graceful_shutdown_sent' -Message "Graceful shutdown signal sent to $ServiceName (PID: $pid)"
    }
    catch {
        Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'graceful_shutdown_error' -Message "Failed to send graceful shutdown: $($_.Exception.Message)"
    }

    # Wait for graceful exit
    $deadline = (Get-Date).AddSeconds($GracefulTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'process_stopped' -Message "$ServiceName (PID: $pid) stopped gracefully"
            return
        }
        Start-Sleep -Milliseconds 200
    }

    # Force kill if still running
    if (-not $Process.HasExited) {
        Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'force_kill' -Message "Force killing $ServiceName (PID: $pid) after ${GracefulTimeoutSeconds}s timeout"
        try {
            $Process.Kill()
            Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'process_killed' -Message "$ServiceName (PID: $pid) terminated"
        }
        catch {
            Write-TiaAgentLog -Level 'ERROR' -Service $ServiceName -InstanceId $RuntimeInstanceId -Event 'kill_error' -Message "Failed to kill $ServiceName (PID: $pid): $($_.Exception.Message)"
        }
    }
}
