function Get-TiaAgentPort {
    <#
    .SYNOPSIS
        Allocates a port for a service, preferring the specified port.
    .PARAMETER ServiceName
        Service name for logging (bridge, opencode)
    .PARAMETER PreferredPort
        Preferred port number
    .PARAMETER RangeStart
        Start of fallback port range
    .PARAMETER RangeEnd
        End of fallback port range
    .OUTPUTS
        Integer port number
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName,

        [Parameter(Mandatory = $true)]
        [int]$PreferredPort,

        [int]$RangeStart = 43100,
        [int]$RangeEnd = 43200
    )

    # Try preferred port first
    if (Test-TiaAgentPortAvailable -Port $PreferredPort) {
        Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -Event 'port_allocated' -Message "Port $PreferredPort available (preferred)"
        return $PreferredPort
    }

    Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -Event 'port_preferred_unavailable' -Message "Preferred port $PreferredPort unavailable, scanning range $RangeStart-$RangeEnd"

    # Scan range for free port
    for ($port = $RangeStart; $port -le $RangeEnd; $port++) {
        if ($port -eq $PreferredPort) { continue }
        if (Test-TiaAgentPortAvailable -Port $port) {
            Write-TiaAgentLog -Level 'INFO' -Service $ServiceName -Event 'port_allocated' -Message "Port $port allocated (fallback)"
            return $port
        }
    }

    throw "No available port in range $RangeStart-$RangeEnd for service $ServiceName"
}

function Test-TiaAgentPortAvailable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        $listener.Stop()
        return $true
    }
    catch {
        return $false
    }
}
