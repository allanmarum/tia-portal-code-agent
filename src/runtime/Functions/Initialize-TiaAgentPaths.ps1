function Initialize-TiaAgentPaths {
    <#
    .SYNOPSIS
        Creates the required directory structure under %LOCALAPPDATA%\TiaAgent.
    .PARAMETER TiaAgentRoot
        Root directory (defaults to %LOCALAPPDATA%\TiaAgent)
    #>
    [CmdletBinding()]
    param(
        [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent')
    )

    $dirs = @(
        $TiaAgentRoot,
        (Join-Path $TiaAgentRoot 'config'),
        (Join-Path $TiaAgentRoot 'runtime'),
        (Join-Path $TiaAgentRoot 'runtime' 'secrets'),
        (Join-Path $TiaAgentRoot 'logs'),
        (Join-Path $TiaAgentRoot 'scripts'),
        (Join-Path $TiaAgentRoot 'temp')
    )

    foreach ($dir in $dirs) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-TiaAgentLog -Level 'INFO' -Event 'directory_created' -Message "Created directory: $dir"
        }
    }

    # Copy scripts from source repo if available
    $repoScripts = Join-Path (Get-Item $PSScriptRoot).Parent.Parent.FullName 'Scripts'
    $targetScripts = Join-Path $TiaAgentRoot 'scripts'

    if (Test-Path $repoScripts) {
        $scriptFiles = @('run.ps1', 'stop.ps1', 'status.ps1')
        foreach ($script in $scriptFiles) {
            $src = Join-Path $repoScripts $script
            $dst = Join-Path $targetScripts $script
            if ((Test-Path $src) -and (-not (Test-Path $dst) -or (Get-Item $src).LastWriteTime -gt (Get-Item $dst).LastWriteTime)) {
                Copy-Item -Path $src -Destination $dst -Force
            }
        }
    }

    Write-TiaAgentLog -Level 'INFO' -Event 'paths_initialized' -Message "TiaAgent paths initialized: $TiaAgentRoot"
}
