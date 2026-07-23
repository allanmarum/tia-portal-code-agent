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
    [ValidateSet("build", "test", "pack-addin", "pack-cli", "verify-addin", "pack-release", "verify-release", "install-dev", "all", "clean", "mcp", "help")]
    [string]$Command = "help",

    [ValidatePattern('^\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+|-dev)?$')]
    [string]$Version,

    [switch]$RequireSigning
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
$tiaAddInPath = "$tiaBasePath\PublicAPI\V21"
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
    # Insert MsBuildVersionArguments before '--' separator (if present)
    # so they are passed to dotnet, not to the application.
    $separatorIndex = [Array]::IndexOf($Arguments, '--')
    if ($separatorIndex -ge 0) {
        $before = $Arguments[0..($separatorIndex - 1)]
        $after = $Arguments[$separatorIndex..($Arguments.Length - 1)]
        & dotnet @before @MsBuildVersionArguments @after
    } else {
        & dotnet @Arguments @MsBuildVersionArguments
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $LASTEXITCODE" }
}

function Ensure-OpcSigner {
    $opcSignerExe = "$Root\tools\OpcSigner\bin\$Config\net48\OpcSigner.exe"
    if (-not (Test-Path $opcSignerExe)) {
        Write-Info "Building OpcSigner tool..."
        Invoke-Dotnet @("build", "$Root\tools\OpcSigner\OpcSigner.csproj", "--configuration", $Config, "--verbosity", "quiet")
    }
}

function Invoke-MsBuildTarget {
    param(
        [Parameter(Mandatory = $true)][string]$Target,
        [string[]]$ExtraArguments = @()
    )
    & dotnet msbuild "$Root\src\TiaAgent.AddIn\TiaAgent.AddIn.csproj" -t:$Target -p:Configuration=$Config @MsBuildVersionArguments @ExtraArguments
    if ($LASTEXITCODE -ne 0) { throw "MSBuild target '$Target' failed with exit code $LASTEXITCODE" }
}

function Invoke-Build {
    Write-Header "BUILD $ProductVersion"
    Write-Step 1 2 "Compiling solution..."
    Invoke-Dotnet @("build", "$Root\TiaAgent.sln", "--configuration", $Config, "--verbosity", "quiet")
    Write-Step 2 2 "Verifying artifacts..."
    foreach ($artifact in @(
        "$Root\src\TiaAgent.AddIn\bin\$Config\net48\TiaAgent.AddIn.dll",
        "$Root\src\TiaAgent.Bridge\bin\$Config\net8.0\TiaAgent.Bridge.dll",
        "$Root\src\TiaAgent.Cli\bin\$Config\net8.0\TiaAgent.Cli.dll"
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
    Ensure-OpcSigner
    $extraArgs = @()
    if ($RequireSigning -or $env:TIA_REQUIRE_SIGNING -eq "true" -or ($ProductVersion -match '^\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+)?$' -and $ProductVersion -notlike '*-dev')) {
        $extraArgs += "-p:RequireSigning=true"
        $env:TIA_REQUIRE_SIGNING = "true"
    }
    Invoke-MsBuildTarget -Target "PackAddIn" -ExtraArguments $extraArgs
}

function Invoke-PackCli {
    Write-Header "PACK CLI $ProductVersion"
    $outputDir = "$Root\artifacts"
    if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }

    # Use a staging directory separate from the Payload source directory
    $payloadDir = "$outputDir\cli-payload"
    if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
    New-Item -ItemType Directory -Path "$payloadDir\Bridge" -Force | Out-Null
    New-Item -ItemType Directory -Path "$payloadDir\AddIn" -Force | Out-Null
    New-Item -ItemType Directory -Path "$payloadDir\config" -Force | Out-Null
    New-Item -ItemType Directory -Path "$payloadDir\notices" -Force | Out-Null

    # 1. Publish Bridge binaries into payload
    Write-Info "Publishing Bridge binaries into payload..."
    Invoke-Dotnet @("publish", "$Root\src\TiaAgent.Bridge\TiaAgent.Bridge.csproj", "--configuration", $Config, "--output", "$payloadDir\Bridge", "--no-restore")

    # Ensure no Siemens assemblies are bundled in Bridge
    Get-ChildItem "$payloadDir\Bridge" -Filter "Siemens.*.dll" | Remove-Item -Force

    # 2. Copy Add-In package if available
    $addinFiles = Get-ChildItem "$outputDir" -Filter "TiaAgent-*.addin" | Sort-Object LastWriteTime -Descending
    if ($addinFiles.Count -gt 0) {
        Write-Info "Bundling Add-In artifact: $($addinFiles[0].Name)"
        Copy-Item $addinFiles[0].FullName "$payloadDir\AddIn\$($addinFiles[0].Name)" -Force
    } else {
        Write-Info "No .addin artifact found in artifacts/. Staging placeholder manifest entry."
    }

    # 3. Copy configuration templates and notices
    if (Test-Path "$Root\config") {
        Copy-Item "$Root\config\*" "$payloadDir\config\" -Recurse -Force
    }
    if (Test-Path "$Root\THIRD_PARTY_NOTICES.md") {
        Copy-Item "$Root\THIRD_PARTY_NOTICES.md" "$payloadDir\notices\" -Force
    }
    if (Test-Path "$Root\LICENSE") {
        Copy-Item "$Root\LICENSE" "$payloadDir\notices\" -Force
    }

    # 4. Generate payload-manifest.json
    Write-Info "Generating payload-manifest.json..."
    $filesList = @()
    $sha256 = [System.Security.Cryptography.SHA256]::Create()

    Get-ChildItem $payloadDir -Recurse -File | ForEach-Object {
        $relPath = $_.FullName.Substring($payloadDir.Length + 1).Replace("\", "/")
        $stream = [System.IO.File]::OpenRead($_.FullName)
        $hashBytes = $sha256.ComputeHash($stream)
        $stream.Close()
        $hashStr = ([System.BitConverter]::ToString($hashBytes)).Replace("-", "").ToLowerInvariant()

        $filesList += @{
            relativePath = $relPath
            sha256Hash = $hashStr
            sizeBytes = $_.Length
        }
    }
    $sha256.Dispose()

    $publisherVersion = $ProductVersion -replace '-.*$', ''
    $addinRelativePath = if (Test-Path "$payloadDir\AddIn") {
        $a = Get-ChildItem "$payloadDir\AddIn" -Filter "*.addin" | Select-Object -First 1
        if ($a) { "AddIn/$($a.Name)" } else { "AddIn/TiaAgent-$publisherVersion.addin" }
    } else { "AddIn/TiaAgent-$publisherVersion.addin" }

    $bridgeMainDll = "$payloadDir\Bridge\TiaAgent.Bridge.dll"
    $bridgeHash = if (Test-Path $bridgeMainDll) {
        $stream = [System.IO.File]::OpenRead($bridgeMainDll)
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $hb = $sha.ComputeHash($stream)
        $stream.Close()
        $sha.Dispose()
        ([System.BitConverter]::ToString($hb)).Replace("-", "").ToLowerInvariant()
    } else { "" }

    # Resolve addin hash and size from the bundled file
    $addinHash = ""
    $addinSize = 0
    if (Test-Path "$payloadDir\AddIn") {
        $addinFile = Get-ChildItem "$payloadDir\AddIn" -Filter "*.addin" | Select-Object -First 1
        if ($addinFile) {
            $addinStream = [System.IO.File]::OpenRead($addinFile.FullName)
            $addinSha = [System.Security.Cryptography.SHA256]::Create()
            $addinHashBytes = $addinSha.ComputeHash($addinStream)
            $addinStream.Close()
            $addinSha.Dispose()
            $addinHash = ([System.BitConverter]::ToString($addinHashBytes)).Replace("-", "").ToLowerInvariant()
            $addinSize = $addinFile.Length
        }
    }

    $manifestData = @{
        schemaVersion = 1
        productVersion = $ProductVersion
        commitSha = $CommitSha
        builtAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
        compatibility = @{
            tiaPortalVersion = "V21"
            opennessVersion = "V21"
            targetFramework = "net8.0"
        }
        components = @{
            bridge = @{
                relativePath = "Bridge/TiaAgent.Bridge.dll"
                version = $ProductVersion
                sha256Hash = $bridgeHash
                sizeBytes = if (Test-Path $bridgeMainDll) { (Get-Item $bridgeMainDll).Length } else { 0 }
            }
            addin = @{
                relativePath = $addinRelativePath
                version = $ProductVersion
                sha256Hash = $addinHash
                sizeBytes = $addinSize
            }
        }
        files = $filesList
    }

    $jsonStr = $manifestData | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText("$payloadDir\payload-manifest.json", $jsonStr)

    # 5. Pack CLI tool
    Invoke-Dotnet @("pack", "$Root\src\TiaAgent.Cli\TiaAgent.Cli.csproj", "--configuration", $Config, "--output", $outputDir)

    # 6. Verify payload in produced .nupkg
    $nupkg = Get-ChildItem "$outputDir" -Filter "TiaAgent.Cli.*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($nupkg) {
        Write-Info "Verifying payload in $($nupkg.Name)..."
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
        try {
            $entries = @($zip.Entries | ForEach-Object { $_.FullName })
            $requiredEntries = @(
                'tools/net8.0/any/payload/payload-manifest.json',
                'tools/net8.0/any/payload/Bridge/TiaAgent.Bridge.dll',
                'tools/net8.0/any/payload/notices/THIRD_PARTY_NOTICES.md'
            )
            foreach ($req in $requiredEntries) {
                if (-not ($entries -contains $req)) {
                    throw "CLI package missing required payload file: $req"
                }
            }
            Write-Ok "CLI package payload verification passed"
        } finally {
            $zip.Dispose()
        }
    }

    Write-Ok "CLI package created at $outputDir"
}

function Invoke-VerifyAddIn {
    Write-Header "VERIFY ADD-IN $ProductVersion"
    Ensure-OpcSigner
    $extraArgs = @()
    if ($RequireSigning -or $env:TIA_REQUIRE_SIGNING -eq "true" -or ($ProductVersion -match '^\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+)?$' -and $ProductVersion -notlike '*-dev')) {
        $extraArgs += "-p:RequireSigning=true"
        $env:TIA_REQUIRE_SIGNING = "true"
    }
    Invoke-MsBuildTarget -Target "VerifyAddIn" -ExtraArguments $extraArgs
}

function Invoke-PackRelease {
    Write-Header "PACK RELEASE METADATA $ProductVersion"
    $outputDir = "$Root\artifacts"
    if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }

    Write-Info "Generating release manifest, SBOM, and SHA256SUMS..."
    Invoke-Dotnet @("run", "--project", "$Root\src\TiaAgent.Cli\TiaAgent.Cli.csproj", "--configuration", $Config, "--", "generate-release-metadata", "--dir", $outputDir, "--version", $ProductVersion, "--commit", $CommitSha, "--repo-root", $Root)

    Write-Ok "Release metadata generated at $outputDir"
}

function Invoke-VerifyRelease {
    Write-Header "VERIFY RELEASE METADATA $ProductVersion"
    $outputDir = "$Root\artifacts"
    if (-not (Test-Path $outputDir)) {
        throw "Release artifacts directory not found: $outputDir"
    }

    Write-Info "Verifying release manifest, SBOM, and SHA256SUMS..."
    Invoke-Dotnet @("run", "--project", "$Root\src\TiaAgent.Cli\TiaAgent.Cli.csproj", "--configuration", $Config, "--", "verify-release", "--dir", $outputDir, "--version", $ProductVersion)

    Write-Ok "Release metadata verification passed"
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
    Write-Host "  build           Compile the solution (no packaging or installation)"
    Write-Host "  test            Run all tests"
    Write-Host "  pack-addin      Package the TIA Portal Add-In (.addin)"
    Write-Host "  pack-cli        Package the CLI global tool (.nupkg)"
    Write-Host "  pack-release    Generate release manifest, SBOM, and SHA256SUMS"
    Write-Host "  verify-addin    Verify the .addin package contents"
    Write-Host "  verify-release  Verify release manifest, SBOM, and SHA256SUMS"
    Write-Host "  install-dev     Deploy the .addin to TIA Portal UserAddIns"
    Write-Host "  all             Build, test, pack-addin, pack-cli, verify-addin, pack-release, verify-release"
    Write-Host "  clean           Remove all build artifacts"
    Write-Host "  mcp             Show MCP server installation instructions"
    Write-Host "  help            Show this help message"
    Write-Host ""
    Write-Host "Resolved version: $ProductVersion"
}

switch ($Command) {
    "build" { Invoke-Build }
    "test" { Invoke-Test }
    "pack-addin" { Invoke-PackAddIn }
    "pack-cli" { Invoke-PackCli }
    "pack-release" { Invoke-PackRelease }
    "verify-addin" { Invoke-VerifyAddIn }
    "verify-release" { Invoke-VerifyRelease }
    "install-dev" { Invoke-InstallDev }
    "mcp" { Invoke-Mcp }
    "clean" { Invoke-Clean }
    "all" { Invoke-Build; Invoke-Test; Invoke-PackAddIn; Invoke-PackCli; Invoke-VerifyAddIn; Invoke-PackRelease; Invoke-VerifyRelease }
    default { Show-Help }
}
