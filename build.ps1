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
    [ValidateSet("build", "test", "pack", "all", "clean", "install", "verify", "mcp", "help")]
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
$MsBuildVersionArguments = @("/p:Version=$ProductVersion", "/p:SourceRevisionId=$CommitSha")

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
function Write-Fail($text) { Write-Host "  FAIL: $text" -ForegroundColor Red }
function Write-Info($text) { Write-Host "  $text" -ForegroundColor Gray }
function Write-Warn($text) { Write-Host "  WARN: $text" -ForegroundColor DarkYellow }

function Stop-StaleDotnetHosts {
    $stale = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($stale) {
        Write-Info "Stopping $($stale.Count) stale dotnet.exe process(es) to release file locks..."
        $stale | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

function Remove-FileWithRetry {
    param([Parameter(Mandatory = $true)][string]$Path, [int]$MaxAttempts = 12, [int]$DelayMilliseconds = 500)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try { Remove-Item -LiteralPath $Path -Force -ErrorAction Stop; return }
        catch {
            if ($attempt -eq $MaxAttempts) { throw "Cannot remove '$Path' after $MaxAttempts attempts: $($_.Exception.Message)" }
            Write-Warn "Package is locked; retrying removal ($attempt/$MaxAttempts)..."
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
}

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & dotnet @Arguments @MsBuildVersionArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $LASTEXITCODE" }
}

function Invoke-Build {
    Write-Header "BUILD $ProductVersion"
    Write-Step 1 3 "Releasing stale file locks..."; Stop-StaleDotnetHosts
    Write-Step 2 3 "Compiling solution..."
    Invoke-Dotnet @("build", "$Root\TiaAgent.sln", "--configuration", $Config, "--verbosity", "quiet")
    Write-Step 3 3 "Verifying artifacts..."
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

function Get-PublisherVersion {
    # Siemens Publisher requires numeric-only versions (X.Y.Z); strip prerelease suffixes.
    return $ProductVersion -replace '-.*', ''
}

function New-VersionedAddInConfig {
    param([string]$Destination)
    $template = "$Root\src\TiaAgent.AddIn\Config.xml"
    $content = [IO.File]::ReadAllText($template)
    if (-not $content.Contains("__PRODUCT_VERSION__")) { throw "Config.xml does not contain __PRODUCT_VERSION__." }
    $publisherVersion = Get-PublisherVersion
    $content = $content.Replace("__PRODUCT_VERSION__", $publisherVersion)
    [IO.File]::WriteAllText($Destination, $content, (New-Object Text.UTF8Encoding($false)))
}

function Get-AddInFile {
    $versionTag = $ProductVersion -replace '[^0-9A-Za-z-]', '-'
    return "$Root\artifacts\TiaAgent-$versionTag.addin"
}

function Invoke-Pack {
    Write-Header "PACKAGING $ProductVersion"
    Add-Type -AssemblyName WindowsBase
    $packDir = "$Root\artifacts"
    $addinFile = Get-AddInFile
    $temporaryAddinFile = "$addinFile.$([Guid]::NewGuid().ToString('N')).addin"
    $addinBin = "$Root\src\TiaAgent.AddIn\bin\$Config\net48"
    $generatedConfig = "$addinBin\Config.xml"
    $publisher = "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\Siemens.Engineering.AddIn.Publisher.exe"

    New-Item -ItemType Directory -Path $packDir -Force | Out-Null
    Remove-FileWithRetry -Path $addinFile
    if (-not (Test-Path "$addinBin\TiaAgent.AddIn.dll")) { throw "TiaAgent.AddIn.dll not found. Run build first." }
    New-VersionedAddInConfig -Destination $generatedConfig

    try {
        if (-not (Test-Path $publisher)) { throw "Publisher not found: $publisher" }
        & $publisher -f $generatedConfig -o $temporaryAddinFile -l "$addinBin\publisher_log.txt" -v -c 2>&1 | ForEach-Object { Write-Info $_ }
        if ($LASTEXITCODE -ne 0) { throw "Siemens Publisher failed." }

        $signerExe = "$Root\tools\OpcSigner\bin\$Config\net48\OpcSigner.exe"
        if (Test-Path $signerExe) {
            & $signerExe $temporaryAddinFile
            if ($LASTEXITCODE -ne 0) { throw "Package signing failed." }
        } else { Write-Warn "OpcSigner not found; package remains unsigned." }

        $package = [IO.Packaging.Package]::Open($temporaryAddinFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $hasFeature = @($package.GetParts() | Where-Object { $_.Uri.ToString() -match 'TIAAGENT\.ADDIN' }).Count -gt 0
            if (-not $hasFeature) { throw "Feature assembly missing from package." }
        } finally { $package.Close(); $package.Dispose() }

        Move-Item -LiteralPath $temporaryAddinFile -Destination $addinFile -Force
        if (Test-Path "$Root\THIRD_PARTY_NOTICES.md") { Copy-Item "$Root\THIRD_PARTY_NOTICES.md" $packDir -Force }
        Write-Ok "Created $addinFile"
        Write-Info "Version: $ProductVersion"
        Write-Info "Commit: $CommitSha"
    } finally {
        if (Test-Path $temporaryAddinFile) { Remove-Item $temporaryAddinFile -Force -ErrorAction SilentlyContinue }
    }
}

function Invoke-Verify {
    Write-Header "VERIFY PACKAGE $ProductVersion"
    Add-Type -AssemblyName WindowsBase
    $addinFile = Get-AddInFile
    if (-not (Test-Path $addinFile)) { throw ".addin file not found: $addinFile" }
    $package = [IO.Packaging.Package]::Open($addinFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $parts = @($package.GetParts() | ForEach-Object { $_.Uri.ToString() })
        foreach ($pattern in @('TIAAGENT\.ADDIN', '^/EngineeringVersion$', '/Meta/', '/Permissions/')) {
            if (-not ($parts -match $pattern)) { throw "Package verification failed for pattern: $pattern" }
        }
    } finally { $package.Close(); $package.Dispose() }
    Write-Ok "Package verification passed"
}

function Invoke-Clean {
    Write-Header "CLEAN"
    Get-ChildItem "$Root\src", "$Root\tests", "$Root\tools" -Directory -Recurse -Include bin,obj -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path "$Root\artifacts") { Remove-Item "$Root\artifacts" -Recurse -Force }
    Write-Ok "Cleanup completed"
}

function Invoke-Install {
    Write-Header "INSTALL $ProductVersion"
    $userAddIns = "$env:APPDATA\Siemens\Automation\Portal V21\UserAddIns"
    New-Item -ItemType Directory -Path $userAddIns -Force | Out-Null
    $addinFile = Get-AddInFile
    if (-not (Test-Path $addinFile)) { throw ".addin file not found. Run pack first." }
    Copy-Item $addinFile $userAddIns -Force
    Write-Ok "Installed to $userAddIns"
}

function Invoke-Mcp {
    Write-Header "MCP SERVER"
    Write-Info "Install: dotnet tool install -g TiaMcpServer"
    Write-Info "Validate: tia-mcp doctor"
}

function Show-Help {
    Write-Header "TIA PORTAL CODE AGENT"
    Write-Host "Usage: .\build.ps1 <command> [-Version X.Y.Z[-channel.N]]"
    Write-Host "Commands: build, test, pack, verify, install, clean, all, mcp, help"
    Write-Host "Resolved version: $ProductVersion"
}

switch ($Command) {
    "build" { Invoke-Build }
    "test" { Invoke-Test }
    "pack" { Invoke-Pack }
    "verify" { Invoke-Verify }
    "mcp" { Invoke-Mcp }
    "clean" { Invoke-Clean }
    "install" { Invoke-Install }
    "all" { Invoke-Build; Invoke-Test; Invoke-Pack }
    default { Show-Help }
}
