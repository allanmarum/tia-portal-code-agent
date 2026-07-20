function Read-TiaAgentSettings {
    <#
    .SYNOPSIS
        Loads and validates the TiaAgent settings.json.
    .PARAMETER SettingsPath
        Path to settings.json (defaults to %LOCALAPPDATA%\TiaAgent\config\settings.json)
    .PARAMETER ExamplePath
        Path to example settings (defaults to repo config/settings.example.json)
    .OUTPUTS
        PSCustomObject with validated settings, or defaults if file missing.
    #>
    [CmdletBinding()]
    param(
        [string]$SettingsPath = (Join-Path $env:LOCALAPPDATA 'TiaAgent' 'config' 'settings.json'),
        [string]$ExamplePath = (Join-Path (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName 'config' 'settings.example.json')
    )

    $defaults = [ordered]@{
        schemaVersion                = 1
        preferredPorts              = [ordered]@{
            bridge   = 43119
            opencode = 43120
        }
        portRange                   = [ordered]@{
            start = 43100
            end   = 43200
        }
        startupTimeoutSeconds       = 60
        healthCheckTimeoutSeconds   = 30
        healthCheckRetryIntervalMs  = 1000
        restartFailedServices       = $false
        maxRestartAttempts          = 3
        logLevel                    = 'Information'
    }

    # Try to load from settings path
    if (Test-Path $SettingsPath) {
        try {
            $json = Get-Content -Path $SettingsPath -Raw -ErrorAction Stop
            $settings = $json | ConvertFrom-Json

            # Validate schema version
            if ($settings.schemaVersion -ne 1) {
                Write-TiaAgentLog -Level 'WARN' -Event 'settings_invalid' -Message "Unsupported schema version: $($settings.schemaVersion). Using defaults."
                return $defaults
            }

            # Validate port range
            $rangeStart = $settings.portRange.start
            $rangeEnd = $settings.portRange.end
            if ($rangeStart -ge $rangeEnd -or $rangeStart -lt 1024 -or $rangeEnd -gt 65535) {
                Write-TiaAgentLog -Level 'WARN' -Event 'settings_invalid' -Message "Invalid port range: $rangeStart-$rangeEnd. Using defaults."
                return $defaults
            }

            # Validate timeouts
            if ($settings.startupTimeoutSeconds -le 0 -or $settings.startupTimeoutSeconds -gt 300) {
                Write-TiaAgentLog -Level 'WARN' -Event 'settings_invalid' -Message "Invalid startupTimeoutSeconds: $($settings.startupTimeoutSeconds). Using defaults."
                return $defaults
            }

            # Validate health check settings
            if ($settings.healthCheckTimeoutSeconds -le 0 -or $settings.healthCheckTimeoutSeconds -gt 120) {
                Write-TiaAgentLog -Level 'WARN' -Event 'settings_invalid' -Message "Invalid healthCheckTimeoutSeconds: $($settings.healthCheckTimeoutSeconds). Using defaults."
                return $defaults
            }

            Write-TiaAgentLog -Level 'INFO' -Event 'settings_loaded' -Message "Settings loaded from: $SettingsPath"
            return $settings
        }
        catch {
            Write-TiaAgentLog -Level 'WARN' -Event 'settings_parse_error' -Message "Failed to parse settings: $($_.Exception.Message). Using defaults."
            return $defaults
        }
    }

    # Try example path
    if (Test-Path $ExamplePath) {
        try {
            $json = Get-Content -Path $ExamplePath -Raw -ErrorAction Stop
            $settings = $json | ConvertFrom-Json
            Write-TiaAgentLog -Level 'INFO' -Event 'settings_loaded_example' -Message "Settings loaded from example: $ExamplePath"
            return $settings
        }
        catch {
            Write-TiaAgentLog -Level 'WARN' -Event 'settings_parse_error' -Message "Failed to parse example settings: $($_.Exception.Message). Using defaults."
        }
    }

    Write-TiaAgentLog -Level 'INFO' -Event 'settings_defaults' -Message "Using default settings"
    return $defaults
}
