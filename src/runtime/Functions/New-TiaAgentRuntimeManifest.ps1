function New-TiaAgentRuntimeManifest {
    <#
    .SYNOPSIS
        Creates or updates the runtime manifest (runtime.json) atomically.
    .PARAMETER TiaAgentRoot
        TiaAgent root directory
    .PARAMETER InstanceId
        Runtime instance ID
    .PARAMETER SupervisorPid
        Supervisor process ID
    .PARAMETER Status
        Runtime status (starting, ready, degraded, stopping, stopped, failed)
    .PARAMETER BridgePort
        Bridge port (0 if not yet allocated)
    .PARAMETER OpenCodePort
        OpenCode port (0 if not yet allocated)
    .PARAMETER BridgeStatus
        Bridge service status
    .PARAMETER OpenCodeStatus
        OpenCode service status
    #>
    [CmdletBinding()]
    param(
        [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),

        [Parameter(Mandatory = $true)]
        [string]$InstanceId,

        [int]$SupervisorPid = $PID,

        [ValidateSet('starting', 'ready', 'degraded', 'stopping', 'stopped', 'failed')]
        [string]$Status = 'starting',

        [int]$BridgePort = 0,
        [int]$OpenCodePort = 0,

        [ValidateSet('pending', 'starting', 'healthy', 'unhealthy', 'stopped', 'failed')]
        [string]$BridgeStatus = 'pending',

        [ValidateSet('pending', 'starting', 'healthy', 'unhealthy', 'stopped', 'failed', 'skipped')]
        [string]$OpenCodeStatus = 'pending',

        [int]$BridgePid = 0,
        [int]$OpenCodePid = 0,

        [string]$RuntimeId = '',
        [string]$RuntimeDisplayName = '',
        [string]$RuntimeMode = '',
        [bool]$RuntimeHealthy = $false
    )

    $now = (Get-Date).ToString('o')
    $runtimeDir = Join-Path $TiaAgentRoot 'runtime'

    if (-not (Test-Path $runtimeDir)) {
        New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
    }

    # Read existing manifest to preserve timestamps
    $existingManifest = $null
    $manifestPath = Join-Path $runtimeDir 'runtime.json'
    if (Test-Path $manifestPath) {
        try {
            $existingManifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        }
        catch {
            Write-TiaAgentLog -Level 'WARN' -Event 'manifest_read_error' -Message "Failed to read existing manifest: $($_.Exception.Message)"
        }
    }

    $startedAt = $now
    if ($existingManifest -and $existingManifest.startedAt) {
        $startedAt = $existingManifest.startedAt
    }

    $bridgeHost = '127.0.0.1'
    $bridgeHealthUrl = if ($BridgePort -gt 0) { "http://${bridgeHost}:${BridgePort}/health" } else { '' }
    $openCodeHost = '127.0.0.1'
    $openCodeHealthUrl = if ($OpenCodePort -gt 0) { "http://${openCodeHost}:${OpenCodePort}/health" } else { '' }

    $manifest = [ordered]@{
        schemaVersion = 1
        instanceId    = $InstanceId
        status        = $Status
        supervisorPid = $SupervisorPid
        startedAt     = $startedAt
        updatedAt     = $now
        services      = [ordered]@{
            bridge = [ordered]@{
                status    = $BridgeStatus
                pid       = $BridgePid
                host      = $bridgeHost
                port      = $BridgePort
                baseUrl   = if ($BridgePort -gt 0) { "http://${bridgeHost}:${BridgePort}" } else { '' }
                healthUrl = $bridgeHealthUrl
            }
            opencode = [ordered]@{
                status    = $OpenCodeStatus
                pid       = $OpenCodePid
                host      = $openCodeHost
                port      = $OpenCodePort
                baseUrl   = if ($OpenCodePort -gt 0) { "http://${openCodeHost}:${OpenCodePort}" } else { '' }
                healthUrl = $openCodeHealthUrl
            }
        }
    }

    # Add runtime info if provided
    if ($RuntimeId) {
        $manifest['runtime'] = [ordered]@{
            id          = $RuntimeId
            displayName = $RuntimeDisplayName
            mode        = $RuntimeMode
            healthy     = $RuntimeHealthy
        }
    }

    # Atomic write: temp file -> validate -> move
    $tempPath = Join-Path $runtimeDir "runtime.json.tmp.$PID"
    $json = $manifest | ConvertTo-Json -Depth 10

    try {
        $json | Out-File -FilePath $tempPath -Encoding UTF8 -Force -ErrorAction Stop

        # Validate the JSON we just wrote
        $null = Get-Content $tempPath -Raw | ConvertFrom-Json -ErrorAction Stop

        # Atomic move
        Move-Item -Path $tempPath -Destination $manifestPath -Force -ErrorAction Stop
        Write-TiaAgentLog -Level 'INFO' -InstanceId $InstanceId -Event 'manifest_updated' -Message "Runtime manifest updated: status=$Status"
    }
    catch {
        # Clean up temp file
        if (Test-Path $tempPath) {
            Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
        }
        Write-TiaAgentLog -Level 'ERROR' -InstanceId $InstanceId -Event 'manifest_write_error' -Message "Failed to write manifest: $($_.Exception.Message)"
        throw
    }
}
