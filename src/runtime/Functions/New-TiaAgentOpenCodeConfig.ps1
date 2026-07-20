function New-TiaAgentOpenCodeConfig {
    <#
    .SYNOPSIS
        Generates a runtime-specific mimocode configuration file.
    .DESCRIPTION
        Creates a .mimocode/mimocode.jsonc config with MCP server configuration for tia-mcp.
        Writes it to a self-contained temp directory so mimo serve picks it up
        without modifying the user's global config or the repo's .mimocode directory.
    .PARAMETER TiaAgentRoot
        TiaAgent root directory
    .PARAMETER SourceConfigPath
        Path to the source opencode.json
    .PARAMETER OpenCodePort
        Allocated OpenCode port
    .PARAMETER McpCommand
        MCP command path (resolved dynamically)
    .OUTPUTS
        PSCustomObject with ConfigPath, WorkingDirectory properties.
    #>
    [CmdletBinding()]
    param(
        [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),

        [string]$SourceConfigPath = '',

        [Parameter(Mandatory = $true)]
        [int]$OpenCodePort,

        [string]$McpCommand = 'tia-mcp'
    )

    # Find source config
    if (-not $SourceConfigPath -or -not (Test-Path $SourceConfigPath)) {
        $repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName
        $SourceConfigPath = Join-Path $repoRoot 'config' 'opencode.json'
    }

    # Read source config if available (for preserving agents/model settings)
    $sourceConfig = $null
    if (Test-Path $SourceConfigPath) {
        try {
            $sourceConfig = Get-Content $SourceConfigPath -Raw | ConvertFrom-Json
        }
        catch {
            Write-TiaAgentLog -Level 'WARN' -Event 'opencode_config_read_error' -Message "Failed to read source config: $($_.Exception.Message)"
        }
    }

    # Resolve MCP command path dynamically
    $resolvedMcpCommand = $McpCommand
    $mcpExe = Get-Command $McpCommand -ErrorAction SilentlyContinue
    if ($mcpExe) {
        $resolvedMcpCommand = $mcpExe.Source
    }

    # Create a self-contained working directory with .mimocode/mimocode.jsonc
    # mimo serve picks up project-level config from the CWD's .mimocode/ directory
    $workDir = Join-Path $TiaAgentRoot 'runtime' 'opencode-workdir'
    $mimoConfigDir = Join-Path $workDir '.mimocode'
    if (-not (Test-Path $mimoConfigDir)) {
        New-Item -ItemType Directory -Path $mimoConfigDir -Force | Out-Null
    }

    # Build the mimocode.jsonc content
    $mcpSection = [ordered]@{
        'tia-portal' = [ordered]@{
            type    = 'local'
            command = @($resolvedMcpCommand)
            enabled = $true
        }
    }

    $generated = [ordered]@{
        server = [ordered]@{
            port = $OpenCodePort
        }
        mcp    = $mcpSection
    }

    # Preserve agents if present in source config
    if ($sourceConfig -and $sourceConfig.agents) {
        $generated.agents = $sourceConfig.agents
    }

    # Preserve model if present in source config
    if ($sourceConfig -and $sourceConfig.model) {
        $generated.model = $sourceConfig.model
    }

    # Write mimocode.jsonc
    $generatedPath = Join-Path $mimoConfigDir 'mimocode.jsonc'
    $json = $generated | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath $generatedPath -Encoding UTF8 -Force

    Write-TiaAgentLog -Level 'INFO' -Event 'mimo_config_generated' -Message "Generated mimo config: $generatedPath (port: $OpenCodePort, workDir: $workDir)"

    return [PSCustomObject]@{
        ConfigPath      = $generatedPath
        WorkingDirectory = $workDir
    }
}
