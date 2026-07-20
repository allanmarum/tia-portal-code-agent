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

    # Start process via cmd.exe /c to handle console allocation properly
    # This avoids issues with CreateNoWindow and missing console for Node.js processes
    $argString = $Arguments -join ' '
    $cmdLine = "`"$resolvedExe`" $argString"

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'cmd.exe'
    $psi.Arguments = "/c `"$cmdLine`""
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true

    # Set environment variables
    foreach ($key in $EnvironmentVariables.Keys) {
        $psi.Environment[$key] = $EnvironmentVariables[$key]
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi

    try {
        # Close stdin immediately to prevent process from waiting for input
        $process.Start() | Out-Null
        try { $process.StandardInput.Close() } catch { }

        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -InstanceId $InstanceId -Event 'process_started' -Message "$ServiceName started with PID $($process.Id)"

        # Wait for process to stay alive for StartupTimeoutSeconds
        $timeout = [TimeSpan]::FromSeconds($StartupTimeoutSeconds)
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($sw.Elapsed -lt $timeout) {
            if ($process.HasExited) {
                # Capture output before throwing
                $exitOutput = ''
                $exitError = ''
                try {
                    $exitOutput = $process.StandardOutput.ReadToEnd()
                    $exitError = $process.StandardError.ReadToEnd()
                } catch { }
                if ($LogFile -and ($exitOutput -or $exitError)) {
                    $ts = [DateTime]::UtcNow.ToString('o')
                    $logContent = ""
                    if ($exitOutput) { $logContent += "[$ts] $exitOutput`n" }
                    if ($exitError) { $logContent += "[$ts] [ERROR] $exitError`n" }
                    try { [System.IO.File]::AppendAllText($LogFile, $logContent) } catch { }
                }
                throw "$ServiceName exited unexpectedly with code $($process.ExitCode). Output: $exitOutput. Error: $exitError"
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
