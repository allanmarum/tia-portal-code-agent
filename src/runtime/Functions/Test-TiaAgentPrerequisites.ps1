function Test-TiaAgentPrerequisites {
    <#
    .SYNOPSIS
        Validates all required prerequisites for the TIA Agent runtime.
    .PARAMETER RuntimeId
        Which runtime to validate (mimo, opencode, claude). Defaults to opencode.
    .OUTPUTS
        PSCustomObject with IsValid, Errors, Warnings properties.
    #>
    [CmdletBinding()]
    param(
        [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),
        [string]$RepoRoot = (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName,
        [string]$RuntimeId = 'opencode'
    )

    $errors = @()
    $warnings = @()

    # 1. Windows environment
    if (-not ($env:OS -eq 'Windows_NT')) {
        $errors += "Not running on Windows (OS: $env:OS)"
    }

    # 2. PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        $errors += "PowerShell 5.1+ required (current: $($PSVersionTable.PSVersion))"
    }

    # 3. dotnet CLI
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        $errors += "dotnet CLI not found. Install .NET SDK 8.0+"
    }
    else {
        # Check SDK version from global.json
        $globalJson = Join-Path $RepoRoot 'global.json'
        if (Test-Path $globalJson) {
            $global = Get-Content $globalJson -Raw | ConvertFrom-Json
            $requiredSdk = $global.sdk.version
            $currentSdk = & dotnet --version 2>&1
            if ($currentSdk -match '^\d+\.\d+') {
                $required = [version]($requiredSdk.Split('+')[0])
                $current = [version]$currentSdk
                if ($current -lt $required) {
                    $warnings += ".NET SDK $requiredSdk+ recommended (current: $currentSdk)"
                }
            }
        }
    }

    # 4. .NET Framework 4.8 (for Add-In)
    $net48Path = "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
    if (-not (Test-Path $net48Path)) {
        $warnings += ".NET Framework 4.8 Developer Pack may not be installed"
    }

    # 5. Bridge executable
    $bridgeDll = Join-Path $RepoRoot 'src\TiaAgent.Bridge\bin\Release\net8.0\TiaAgent.Bridge.dll'
    if (-not (Test-Path $bridgeDll)) {
        # Try debug
        $bridgeDll = Join-Path $RepoRoot 'src\TiaAgent.Bridge\bin\Debug\net8.0\TiaAgent.Bridge.dll'
        if (-not (Test-Path $bridgeDll)) {
            $errors += "Bridge executable not found. Run: dotnet build TiaAgent.sln -c Release"
        }
    }

    # 6. Runtime executable (runtime-specific)
    $runtimeExe = switch ($RuntimeId) {
        'mimo'     { 'mimo' }
        'opencode' { 'opencode' }
        'claude'   { 'claude' }
        default    { $RuntimeId }
    }
    $runtimeCmd = Get-Command $runtimeExe -ErrorAction SilentlyContinue
    if (-not $runtimeCmd) {
        # Fallback: opencode can use mimo if opencode CLI is not found
        if ($RuntimeId -eq 'opencode') {
            $mimoCmd = Get-Command mimo -ErrorAction SilentlyContinue
            if ($mimoCmd) {
                $runtimeCmd = $mimoCmd
            }
        }
    }
    if (-not $runtimeCmd) {
        $errors += "Runtime executable '$runtimeExe' not found for runtime '$RuntimeId'"
    }

    # 7. MCP server (tia-mcp) — only needed for opencode runtime
    if ($RuntimeId -eq 'opencode') {
        $tiaMcp = Get-Command tia-mcp -ErrorAction SilentlyContinue
        if (-not $tiaMcp) {
            $warnings += "tia-mcp not found. Install: dotnet tool install -g TiaMcpServer"
        }
    }

    # 8. TiaAgent directory writable
    if (-not (Test-Path $TiaAgentRoot)) {
        try {
            New-Item -ItemType Directory -Path $TiaAgentRoot -Force | Out-Null
        }
        catch {
            $errors += "Cannot create TiaAgent directory: $TiaAgentRoot"
        }
    }

    $testFile = Join-Path $TiaAgentRoot '.write_test'
    try {
        'test' | Out-File -FilePath $testFile -Force -ErrorAction Stop
        Remove-Item -Path $testFile -Force -ErrorAction SilentlyContinue
    }
    catch {
        $errors += "TiaAgent directory is not writable: $TiaAgentRoot"
    }

    # 9. Runtime-specific config (opencode needs config/opencode.json)
    if ($RuntimeId -eq 'opencode') {
        $opencodeConfig = Join-Path $RepoRoot 'config\opencode.json'
        if (-not (Test-Path $opencodeConfig)) {
            $warnings += "OpenCode config not found: config\opencode.json"
        }
        else {
            try {
                Get-Content $opencodeConfig -Raw | ConvertFrom-Json | Out-Null
            }
            catch {
                $warnings += "OpenCode config is invalid JSON: config\opencode.json"
            }
        }
    }

    # 10. Settings file
    $settingsPath = Join-Path $TiaAgentRoot 'config\settings.json'
    if (Test-Path $settingsPath) {
        try {
            Get-Content $settingsPath -Raw | ConvertFrom-Json | Out-Null
        }
        catch {
            $warnings += "Settings file is invalid JSON: $settingsPath"
        }
    }

    $isValid = $errors.Count -eq 0
    return [PSCustomObject]@{
        IsValid  = $isValid
        Errors   = $errors
        Warnings = $warnings
    }
}
