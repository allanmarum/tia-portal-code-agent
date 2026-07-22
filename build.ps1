#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Portal Code Agent build, test, packaging, verification, and installation tool.

.EXAMPLE
    .\build.ps1 all
    .\build.ps1 all -Version 0.2.0-beta.1
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "pack-addin", "pack-cli", "verify-addin", "install-dev", "all", "clean", "mcp", "help")]
    [string]$Command = "help",

    [ValidatePattern('^\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+|-dev)?$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Config = "Release"

function Resolve-ProductVersion {
    param([string]$ExplicitVersion)

    if ($ExplicitVersion) { return $ExplicitVersion }
    if ($env:TIA_AGENT_VERSION) { return $env:TIA_AGENT_VERSION }

    if ($env:GITHUB_REF_TYPE -eq "tag" -and $env:GITHUB_REF_NAME -match '^v(?<version>\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+)?)$') {
        return $Matches.version
    }

    try {
        $tag = (& git -C $Root describe --tags --exact-match HEAD 2>$null).Trim()
        if ($LASTEXITCODE -eq 0 -and $tag -match '^v(?<version>\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+)?)$') {
            return $Matches.version
        }
    } catch { }

    return "0.0.0-dev"
}

function Resolve-CommitSha {
    if ($env:GITHUB_SHA) { return $env:GITHUB_SHA }
    try {
        $sha = (& git -C $Root rev-parse HEAD 2>$null).Trim()
        if ($LASTEXITCODE -eq 0 -and $sha) { return $sha }
    } catch { }
    return "unknown"
}

$ProductVersion = Resolve-ProductVersion -ExplicitVersion $Version
$CommitSha = Resolve-CommitSha
$MsBuildVersionArguments = @("-p:Version=$ProductVersion", "-p:SourceRevisionId=$CommitSha")

# Auto-detect TIA Portal V21 assemblies.
$tiaBasePath = "C:\Program Files\Siemens\Automation\Portal V21"
$tiaNet48Path = "$tiaBasePath\PublicAPI\V21\net48"
$tiaAddInPath = "$tiaBasePath\PublicAPI\V21.AddIn"
if (Test-Path "$tiaNet48Path\Siemens.Engineering.Base.dll") {
    $env:TiaPublicApiDir = $tiaNet48Path
    $env:SiemensAssembliesExist = "true"
    Write-Host "  TIA Openness V21 detected: $tiaNet48Path" -ForegroundColor Gray
}
if (Test-Path "$tiaAddInPath\Siemens.Engineering.AddIn.Base.dll") {
    $env:TiaAddInApiDir = $tiaAddInPath
    Write-Host "  TIA Add-In V21 detected: $tiaAddInPath" -ForegroundColor Gray
}

function Write-Header($text) {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
}
function Write-Step($step, $total, $text) { Write-Host "[$step/$total] $text" -ForegroundColor Yellow }
function Write-Ok($text) { Write-Host "  OK: $text" -ForegroundColor Green }
function Write-Info($text) { Write-Host "  $text" -ForegroundColor Gray }

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & dotnet @Arguments @MsBuildVersionArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $LASTEXITCODE" }
}

function Invoke-MsBuildTarget {
    param([Parameter(Mandatory = $true)][string]$Target)
    & dotnet msbuild "$Root\src\TiaAgent.AddIn\TiaAgent.AddIn.csproj" -t:$Target -p:Configuration=$Config @MsBuildVersionArguments
    if ($LASTEXITCODE -ne 0) { throw "MSBuild target '$Target' failed with exit code $LASTEXITCODE" }
}

function Invoke-Build {
    Write-Header "BUILD $ProductVersion"
    Write-Step 1 2 "Compiling solution..."
    Invoke-Dotnet @("build", "$Root\TiaAgent.sln", "--configuration", $Config, "--verbosity", "quiet")
    Write-Step 2 2 "Verifying artifacts..."
    foreach ($artifact in @(
        "$Root\src\TiaAgent.AddIn\bin\$Config\net48\TiaAgent.AddIn.dll",
        "$Root\src\TiaAgent.Bridge\bin\$Config\net8.0\TiaAgent.Bridge.dll"
    )) {
        if (-not (Test-Path $artifact)) { throw "Expected build artifact not found: $artifact" }
        Write-Ok (Split-Path $artifact -Leaf)
    }
}

function Invoke-Test {
    Write-Header "TESTS $ProductVersion"
    Invoke-Dotnet @("test", "$Root\TiaAgent.sln", "--configuration", $Config, "--verbosity", "normal", "--no-restore")
    Write-Ok "All tests passed"
}

function Invoke-PackAddIn {
    Write-Header "PACK ADD-IN $ProductVersion"
    Invoke-MsBuildTarget -Target "PackAddIn"
}

function Invoke-PackCli {
    Write-Header "PACK CLI $ProductVersion"
    throw "pack-cli is not yet implemented. Will be added when the CLI global tool is created (REL-010)."
}

function Invoke-VerifyAddIn {
    Write-Header "VERIFY ADD-IN $ProductVersion"
    Invoke-MsBuildTarget -Target "VerifyAddIn"
}

function Invoke-InstallDev {
    Write-Header "INSTALL DEV $ProductVersion"
    Invoke-MsBuildTarget -Target "InstallAddIn"
}

function Invoke-Clean {
    Write-Header "CLEAN"
    Get-ChildItem "$Root\src", "$Root\tests", "$Root\tools" -Directory -Recurse -Include bin,obj -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path "$Root\artifacts") { Remove-Item "$Root\artifacts" -Recurse -Force }
    Write-Ok "Cleanup completed"
}

function Invoke-Mcp {
    Write-Header "MCP SERVER"
    Write-Info "Install: dotnet tool install -g TiaMcpServer"
    Write-Info "Validate: tia-mcp doctor"
}

function Show-Help {
    Write-Header "TIA PORTAL CODE AGENT"
    Write-Host "Usage: .\build.ps1 <command> [-Version X.Y.Z[-channel.N]]"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  build         Compile the solution (no packaging or installation)"
    Write-Host "  test          Run all tests"
    Write-Host "  pack-addin    Package the TIA Portal Add-In (.addin)"
    Write-Host "  pack-cli      Package the CLI (not yet implemented)"
    Write-Host "  verify-addin  Verify the .addin package contents"
    Write-Host "  install-dev   Deploy the .addin to TIA Portal UserAddIns"
    Write-Host "  all           Build, test, and pack-addin in sequence"
    Write-Host "  clean         Remove all build artifacts"
    Write-Host "  mcp           Show MCP server installation instructions"
    Write-Host "  help          Show this help message"
    Write-Host ""
    Write-Host "Resolved version: $ProductVersion"
}

switch ($Command) {
    "build" { Invoke-Build }
    "test" { Invoke-Test }
    "pack-addin" { Invoke-PackAddIn }
    "pack-cli" { Invoke-PackCli }
    "verify-addin" { Invoke-VerifyAddIn }
    "install-dev" { Invoke-InstallDev }
    "mcp" { Invoke-Mcp }
    "clean" { Invoke-Clean }
    "all" { Invoke-Build; Invoke-Test; Invoke-PackAddIn }
    default { Show-Help }
}
