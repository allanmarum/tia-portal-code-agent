function Start-TiaAgentService {
    <#
    .SYNOPSIS
        Starts a service process with captured PID.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName,

        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [string[]]$Arguments = @(),

        [string]$WorkingDirectory = (Get-Location).Path,

        [hashtable]$EnvironmentVariables = @{},

        [string]$LogFile = '',

        [int]$StartupTimeoutSeconds = 30,

        [string]$InstanceId = ''
    )

    # Validate executable exists
    $resolvedExe = $ExecutablePath
    if (-not (Test-Path $ExecutablePath)) {
        $cmd = Get-Command $ExecutablePath -ErrorAction SilentlyContinue
        if ($cmd) {
            $resolvedExe = $cmd.Source
        } else {
            throw "Executable not found: $ExecutablePath"
        }
    }

    Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $InstanceId -Event 'process_starting' -Message "Starting ${ServiceName}: ${ExecutablePath} $($Arguments -join ' ')"

    # Ensure log directory exists
    if ($LogFile) {
        $logDir = Split-Path $LogFile -Parent
        if (-not (Test-Path $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
    }

    # Start process directly (no cmd.exe wrapper).
    # We avoid cmd.exe wrapping because it changes process lifecycle:
    # when node.exe spawns child processes (like mimo), cmd.exe may exit
    # before the child finishes, leaving orphaned processes.
    # For .ps1 scripts, we still use powershell.exe as the host.
    $argString = $Arguments -join ' '
    $psi = New-Object System.Diagnostics.ProcessStartInfo

    if ($resolvedExe.EndsWith('.ps1', [System.StringComparison]::OrdinalIgnoreCase)) {
        $psi.FileName = 'powershell.exe'
        $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$resolvedExe`" $argString"
    } else {
        $psi.FileName = $resolvedExe
        $psi.Arguments = $argString
    }
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    # Set environment variables
    foreach ($key in $EnvironmentVariables.Keys) {
        $psi.Environment[$key] = $EnvironmentVariables[$key]
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi

    try {
        $process.Start() | Out-Null

        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $InstanceId -Event 'process_started' -Message "$ServiceName started with PID $($process.Id)"

        # Wait for process to stay alive for StartupTimeoutSeconds
        $timeout = [TimeSpan]::FromSeconds($StartupTimeoutSeconds)
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($sw.Elapsed -lt $timeout) {
            if ($process.HasExited) {
                $logHint = ''
                if ($LogFile -and (Test-Path $LogFile)) {
                    $logHint = ". Check log: $LogFile"
                }
                throw "$ServiceName exited unexpectedly with code $($process.ExitCode)$logHint"
            }
            Start-Sleep -Milliseconds 100
        }

        return [PSCustomObject]@{
            Process = $process
            Pid     = $process.Id
        }
    }
    catch {
        try {
            if ($process -and -not $process.HasExited) {
                $process.Kill()
            }
        } catch { }
        throw "Failed to start ${ServiceName}: $($_.Exception.Message)"
    }
}
