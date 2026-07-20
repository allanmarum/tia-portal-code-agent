function Wait-TiaAgentHealth {
    <#
    .SYNOPSIS
        Polls a health endpoint with bounded retries.
    .PARAMETER Url
        Health endpoint URL
    .PARAMETER TimeoutSeconds
        Maximum time to wait for healthy response
    .PARAMETER RetryIntervalMs
        Interval between retries in milliseconds
    .PARAMETER ExpectedService
        Expected service name in health response
    .PARAMETER ServiceName
        Service name for logging
    .OUTPUTS
        Boolean indicating if service is healthy
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [int]$TimeoutSeconds = 30,
        [int]$RetryIntervalMs = 1000,
        [string]$ExpectedService = '',
        [string]$ServiceName = 'service'
    )

    $maxRetries = [math]::Ceiling($TimeoutSeconds / ($RetryIntervalMs / 1000))
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -Event 'health_check_start' -Message "Waiting for $ServiceName health at $Url (timeout: ${TimeoutSeconds}s)"

    for ($i = 0; $i -lt $maxRetries; $i++) {
        try {
            $response = Invoke-RestMethod -Uri $Url -TimeoutSec 5 -Method Get -ErrorAction Stop

            # Check service identity if expected
            if ($ExpectedService -and $response.service) {
                if ($response.service -ne $ExpectedService) {
                    Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -Event 'health_check_mismatch' -Message "Expected service '$ExpectedService' but got '$($response.service)'"
                    continue
                }
            }

            # Check status
            $status = $response.status
            if ($status -eq 'healthy' -or $status -eq 'ok') {
                Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -Event 'health_check_ok' -Message "$ServiceName is healthy after $i retries"
                return $true
            }

            Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -Event 'health_check_pending' -Message "$ServiceName status: $status (attempt $($i + 1)/$maxRetries)"
        }
        catch {
            Write-TiaAgentLog -Level 'WARN' -Service $ServiceName -Event 'health_check_error' -Message "$ServiceName health check failed (attempt $($i + 1)/$maxRetries): $($_.Exception.Message)"
        }

        if ((Get-Date) -ge $deadline) {
            break
        }

        Start-Sleep -Milliseconds $RetryIntervalMs
    }

    Write-TiaAgentLog -Level 'ERROR' -Service $ServiceName -Event 'health_check_timeout' -Message "$ServiceName health check timed out after ${TimeoutSeconds}s"
    return $false
}
