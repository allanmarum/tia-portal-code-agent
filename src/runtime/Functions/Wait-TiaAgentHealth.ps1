function Wait-TiaAgentHealth {
    <#
    .SYNOPSIS
        Polls a health endpoint with bounded retries.
    .DESCRIPTION
        Checks if a service is healthy by connecting to its health endpoint.
        For services that return non-200 status codes (like mimo's 503 for
        "Web UI unavailable"), falls back to TCP connectivity check.
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

    # Extract host and port from URL for TCP fallback
    $uri = [System.Uri]$Url
    $tcpHost = $uri.Host
    $tcpPort = $uri.Port

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
            # For non-2xx responses (like 503), check if the server is at least
            # accepting connections via TCP. Some servers (like mimo) return 503
            # for their health endpoint when certain components are unavailable,
            # but the server itself is running and ready to accept requests.
            $isWebResponse = $_.Exception -is [System.Net.WebException]
            if ($isWebResponse -and $_.Exception.Response) {
                $statusCode = [int]$_.Exception.Response.StatusCode
                if ($statusCode -ge 500) {
                    # Server is responding (even with error) - check TCP connectivity
                    $tcpOk = Test-TcpConnectivity -Host $tcpHost -Port $tcpPort
                    if ($tcpOk) {
                        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -Event 'health_check_ok' -Message "$ServiceName is accepting connections (HTTP $statusCode, TCP OK) after $i retries"
                        return $true
                    }
                }
            }

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

function Test-TcpConnectivity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Host,

        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.Connect($Host, $Port)
        $client.Close()
        return $true
    }
    catch {
        return $false
    }
}
