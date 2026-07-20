function Write-TiaAgentLog {
    <#
    .SYNOPSIS
        Writes structured log entries to the supervisor log file.
    .PARAMETER Level
        Log level: INFO, WARN, ERROR, DEBUG, STARTUP
    .PARAMETER Message
        Log message
    .PARAMETER Service
        Service name (bridge, opencode, supervisor)
    .PARAMETER InstanceId
        Runtime instance ID
    .PARAMETER CorrelationId
        Correlation ID for request tracing
    .PARAMETER Event
        Event type (e.g., startup, health_check, process_start)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('INFO', 'WARN', 'ERROR', 'DEBUG', 'STARTUP')]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [string]$Message,

        [string]$Service = 'supervisor',

        [string]$InstanceId = '',

        [string]$CorrelationId = '',

        [string]$Event = ''
    )

    $tiaAgentDir = Join-Path $env:LOCALAPPDATA 'TiaAgent'
    $logDir = Join-Path $tiaAgentDir 'logs'
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }

    $logFile = Join-Path $logDir 'supervisor.log'
    $timestamp = (Get-Date).ToString('o')

    $entry = [ordered]@{
        timestamp    = $timestamp
        level        = $Level
        service      = $Service
        instanceId   = $InstanceId
        correlationId = $CorrelationId
        event        = $Event
        message      = $Message
    }
    $json = $entry | ConvertTo-Json -Compress
    $line = "$json`n"

    try {
        [System.IO.File]::AppendAllText($logFile, $line)
    }
    catch {
        Write-Warning "Failed to write log: $_"
    }
}

Set-Alias -Name Write-Log -Value Write-TiaAgentLog
