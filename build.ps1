#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Portal Code Agent - Build, test, and packaging tool.

.DESCRIPTION
    Generates the correct .addin (OPC) package for installation in TIA Portal V21.
    Usage: .\build.ps1 [command]

.COMMANDS
    build       - Compiles the solution
    test        - Runs the tests
    pack        - Packages the Add-In (.addin OPC package)
    all         - Build + Test + Pack
    clean       - Cleans build artifacts
    install     - Copies to TIA Portal Add-Ins folder
    verify      - Verifies the .addin package contents

.EXAMPLE
    .\build.ps1 build
    .\build.ps1 all
    .\build.ps1 pack
    .\build.ps1 verify
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "pack", "all", "clean", "install", "verify", "mcp", "help")]
    [string]$Command = "help"
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Config = "Release"
$Version = "0.1.0"

# ============================================================
# Auto-detect TIA Portal V21 assemblies
# ============================================================
$tiaBasePath = "C:\Program Files\Siemens\Automation\Portal V21"
$tiaNet48Path = "$tiaBasePath\PublicAPI\V21\net48"
$tiaAddInPath = "$tiaBasePath\PublicAPI\V21.AddIn"

$siemensDetected = $false
if (Test-Path "$tiaNet48Path\Siemens.Engineering.Base.dll") {
    $env:TiaPublicApiDir = $tiaNet48Path
    $siemensDetected = $true
    Write-Host "  TIA Openness V21 detected: $tiaNet48Path" -ForegroundColor Gray
}
if (Test-Path "$tiaAddInPath\Siemens.Engineering.AddIn.Base.dll") {
    $env:TiaAddInApiDir = $tiaAddInPath
    Write-Host "  TIA Add-In V21 detected: $tiaAddInPath" -ForegroundColor Gray
}
if ($siemensDetected) {
    $env:SiemensAssembliesExist = "true"
}

# ============================================================
# Helper functions
# ============================================================

function Write-Header($text) {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step($step, $total, $text) {
    Write-Host "[$step/$total] $text" -ForegroundColor Yellow
}

function Write-Ok($text) {
    Write-Host "  OK: $text" -ForegroundColor Green
}

function Write-Fail($text) {
    Write-Host "  FAIL: $text" -ForegroundColor Red
}

function Write-Info($text) {
    Write-Host "  $text" -ForegroundColor Gray
}

function Write-Warn($text) {
    Write-Host "  WARN: $text" -ForegroundColor DarkYellow
}

function Stop-StaleDotnetHosts {
    $stale = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($stale) {
        Write-Info "Stopping $($stale.Count) stale dotnet.exe process(es) to release file locks..."
        $stale | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

function Remove-FileWithRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [ValidateRange(1, 60)]
        [int]$MaxAttempts = 12,

        [ValidateRange(50, 5000)]
        [int]$DelayMilliseconds = 500
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    # Release any package handles left behind in the current PowerShell process.
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw "Cannot remove '$Path' after $MaxAttempts attempts. Another process is still using the file. Close TIA Portal, File Explorer preview/archive tools, and any other build or verify process, then retry. Original error: $($_.Exception.Message)"
            }

            Write-Warn "Package is locked; retrying removal ($attempt/$MaxAttempts)..."
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
}

# ============================================================
# Build
# ============================================================

function Invoke-Build {
    Write-Header "BUILD"
    Write-Step 1 4 "Releasing stale file locks..."
    Stop-StaleDotnetHosts

    Write-Step 2 4 "Compiling solution..."
    dotnet build "$Root\TiaAgent.sln" --configuration $Config --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Write-Fail "Build failed"; exit 1 }
    Write-Ok "Solution compiled"

    Write-Step 3 4 "Verifying projects..."
    $projects = Get-ChildItem "$Root\src\*\*.csproj" -ErrorAction SilentlyContinue
    Write-Ok "$($projects.Count) source projects found"

    $tests = Get-ChildItem "$Root\tests\*\*.csproj" -ErrorAction SilentlyContinue
    Write-Ok "$($tests.Count) test projects found"

    Write-Step 4 4 "Verifying artifacts..."
    $addinDll = "$Root\src\TiaAgent.AddIn\bin\$Config\net48\TiaAgent.AddIn.dll"
    if (Test-Path $addinDll) {
        Write-Ok "TiaAgent.AddIn.dll built"
    } else {
        Write-Fail "TiaAgent.AddIn.dll not found at: $addinDll"
        exit 1
    }

    $bridgeDll = "$Root\src\TiaAgent.Bridge\bin\$Config\net8.0\TiaAgent.Bridge.dll"
    if (Test-Path $bridgeDll) {
        Write-Ok "TiaAgent.Bridge.dll built"
    } else {
        Write-Fail "TiaAgent.Bridge.dll not found at: $bridgeDll"
        exit 1
    }

    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
}

# ============================================================
# Test
# ============================================================

function Invoke-Test {
    Write-Header "TESTS"
    Write-Step 1 2 "Running tests..."

    dotnet test "$Root\TiaAgent.sln" --configuration $Config --verbosity normal
    if ($LASTEXITCODE -ne 0) { Write-Fail "Tests failed"; exit 1 }

    Write-Step 2 2 "Results"
    Write-Ok "All tests passed"
}

# ============================================================
# Pack -- Creates a proper OPC (.addin) package
# ============================================================

function Invoke-Pack {
    Write-Header "PACKAGING"

    Add-Type -AssemblyName WindowsBase

    $packDir = "$Root\artifacts"
    $versionTag = $Version -replace '\.', '-'
    $addinFile = "$packDir\TiaAgent-$versionTag.addin"
    $temporaryAddinFile = "$packDir\TiaAgent-$versionTag.$([Guid]::NewGuid().ToString('N')).tmp.addin"
    $addinBin = "$Root\src\TiaAgent.AddIn\bin\$Config\net48"
    $configXml = "$Root\src\TiaAgent.AddIn\Config.xml"
    $publisher = "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\Siemens.Engineering.AddIn.Publisher.exe"

    # Clean
    Write-Step 1 4 "Cleaning previous packages..."
    if (-not (Test-Path -LiteralPath $packDir)) {
        New-Item -ItemType Directory -Path $packDir -Force | Out-Null
    }
    Remove-FileWithRetry -Path $addinFile
    Write-Ok "Clean"

    # Verify build output
    if (-not (Test-Path "$addinBin\TiaAgent.AddIn.dll")) {
        Write-Fail "TiaAgent.AddIn.dll not found. Run: .\build.ps1 build"
        exit 1
    }

    # Copy Config.xml to build dir
    Copy-Item $configXml "$addinBin\Config.xml" -Force

    try {
        # Step 2: Run Siemens Publisher into a temporary file. This prevents
        # a failed build from leaving a partial package at the final path.
        Write-Step 2 4 "Running Siemens Publisher..."
        if (-not (Test-Path $publisher)) {
            Write-Fail "Publisher not found: $publisher"
            exit 1
        }
        & $publisher -f "$addinBin\Config.xml" -o $temporaryAddinFile -l "$addinBin\publisher_log.txt" -v -c 2>&1 | ForEach-Object { Write-Info $_ }
        if ($LASTEXITCODE -ne 0) { Write-Fail "Publisher failed"; exit 1 }
        Write-Ok "Publisher created base package"

        # Step 3: Sign the temporary package
        Write-Step 3 4 "Signing package..."
        $signerExe = "$Root\tools\OpcSigner\bin\$Config\net48\OpcSigner.exe"
        if (Test-Path $signerExe) {
            & $signerExe $temporaryAddinFile 2>&1 | ForEach-Object { Write-Info $_ }
            if ($LASTEXITCODE -ne 0) { Write-Fail "Package signing failed"; exit 1 }
            Write-Ok "Package signed"
        } else {
            Write-Info "OpcSigner not found -- package will be unsigned"
        }

        # Step 4: Verify and always dispose the OPC package handle.
        Write-Step 4 4 "Verifying package..."
        $pkg = $null
        try {
            $pkg = [System.IO.Packaging.Package]::Open(
                $temporaryAddinFile,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::Read
            )

            $partCount = 0
            $hasFeature = $false
            foreach ($part in $pkg.GetParts()) {
                $partCount++
                if ($part.Uri.ToString() -match "TIAAGENT.ADDIN") { $hasFeature = $true }
            }
        }
        finally {
            if ($null -ne $pkg) {
                $pkg.Close()
                $pkg.Dispose()
            }
        }

        if ($hasFeature) { Write-Ok "Feature assembly present" }
        else { Write-Fail "Feature assembly missing!"; exit 1 }
        Write-Ok "Total parts: $partCount"

        # Publish only after signing and verification have completed.
        Move-Item -LiteralPath $temporaryAddinFile -Destination $addinFile -Force -ErrorAction Stop

        # Summary
        $size = (Get-Item $addinFile).Length / 1KB
        Write-Info "File: $addinFile"
        Write-Info "Size: $([math]::Round($size, 1)) KB"
        Write-Info "Version: $Version"
        Write-Info "Publisher: Siemens.Engineering.AddIn.Publisher.exe"

        Write-Host ""
        Write-Host "Packaging completed!" -ForegroundColor Green
        Write-Host "  To install: .\build.ps1 install" -ForegroundColor Gray
        Write-Host "  To verify:  .\build.ps1 verify" -ForegroundColor Gray
    }
    finally {
        # Remove an incomplete temporary package after any failure.
        if (Test-Path -LiteralPath $temporaryAddinFile) {
            Remove-Item -LiteralPath $temporaryAddinFile -Force -ErrorAction SilentlyContinue
        }
    }
}

# ============================================================
# Verify -- Checks the .addin package contents
# ============================================================

function Invoke-Verify {
    Write-Header "VERIFY PACKAGE"

    Add-Type -AssemblyName WindowsBase

    $packDir = "$Root\artifacts"
    $versionTag = $Version -replace '\.', '-'
    $addinFile = "$packDir\TiaAgent-$versionTag.addin"

    if (-not (Test-Path $addinFile)) {
        Write-Fail ".addin file not found: $addinFile"
        Write-Info "Run: .\build.ps1 pack"
        exit 1
    }

    Write-Step 1 4 "Opening package..."
    $pkg = $null
    try {
        $pkg = [System.IO.Packaging.Package]::Open(
            $addinFile,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read
        )
        Write-Ok "Package opened"

        Write-Step 2 4 "Checking parts..."
        $parts = $pkg.GetParts()
        $partList = @()
        $hasFeature = $false
        $hasEngineeringVersion = $false
        $hasMeta = $false
        $hasPermissions = $false

        foreach ($part in $parts) {
            $uri = $part.Uri.ToString()
            $partList += $uri
            if ($uri -match "TIAAGENT\.ADDIN") { $hasFeature = $true }
            if ($uri -eq "/EngineeringVersion") { $hasEngineeringVersion = $true }
            if ($uri -match "/Meta/") { $hasMeta = $true }
            if ($uri -match "/Permissions/") { $hasPermissions = $true }
        }
    }
    finally {
        if ($null -ne $pkg) {
            $pkg.Close()
            $pkg.Dispose()
        }
    }

    Write-Info "Total parts: $($partList.Count)"

    $checks = @(
        @{ Name = "Feature assembly (TiaAgent.AddIn.dll)"; Ok = $hasFeature },
        @{ Name = "EngineeringVersion"; Ok = $hasEngineeringVersion },
        @{ Name = "Meta metadata"; Ok = $hasMeta },
        @{ Name = "Permissions"; Ok = $hasPermissions }
    )

    $allOk = $true
    foreach ($check in $checks) {
        if ($check.Ok) { Write-Ok $check.Name }
        else { Write-Fail $check.Name; $allOk = $false }
    }

    # List all parts
    Write-Step 3 4 "Parts listing:"
    foreach ($uri in ($partList | Sort-Object)) {
        Write-Info "  $uri"
    }

    Write-Step 4 4 "Result"
    if ($allOk) {
        Write-Host ""
        Write-Host "Package verification PASSED!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "Package verification FAILED -- missing required parts" -ForegroundColor Red
        exit 1
    }
}

# ============================================================
# MCP -- Shows info about the Czarnak MCP server
# ============================================================

function Invoke-Mcp {
    Write-Header "MCP SERVER (Czarnak/tia-portal-mcp)"
    Write-Info "This project uses Czarnak's TiaMcpServer as the MCP server."
    Write-Host ""
    Write-Host "Install:   dotnet tool install -g TiaMcpServer" -ForegroundColor Cyan
    Write-Host "Validate:  tia-mcp doctor" -ForegroundColor Cyan
    Write-Host "Inspect:   npx -y @modelcontextprotocol/inspector tia-mcp" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The MCP server is launched automatically by OpenCode via stdio transport." -ForegroundColor Gray
    Write-Host "Config: config/opencode.json" -ForegroundColor Gray
}

# ============================================================
# Clean
# ============================================================

function Invoke-Clean {
    Write-Header "CLEAN"

    Write-Step 1 3 "Removing bin/ and obj..."
    Get-ChildItem "$Root\src" -Directory -Recurse -Include "bin", "obj" |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem "$Root\tests" -Directory -Recurse -Include "bin", "obj" |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Write-Ok "Build directories removed"

    Write-Step 2 3 "Removing artifacts..."
    if (Test-Path "$Root\artifacts") {
        Remove-Item "$Root\artifacts" -Recurse -Force
    }
    Write-Ok "Artifacts removed"

    Write-Step 3 3 "Cleanup completed"
    Write-Ok "Ready for rebuild"
}

# ============================================================
# Install
# ============================================================

function Invoke-Install {
    Write-Header "INSTALL TO TIA PORTAL"

    $userAddIns = "$env:APPDATA\Siemens\Automation\Portal V21\UserAddIns"

    if (!(Test-Path $userAddIns)) {
        Write-Warn "Add-Ins folder not found: $userAddIns"
        Write-Info "Creating folder (TIA Portal will scan it on startup)..."
        New-Item -ItemType Directory -Path $userAddIns -Force | Out-Null
    }

    $packDir = "$Root\artifacts"
    $versionTag = $Version -replace '\.', '-'
    $addinFile = "$packDir\TiaAgent-$versionTag.addin"

    if (!(Test-Path $addinFile)) {
        Write-Fail ".addin file not found. Run: .\build.ps1 pack"
        exit 1
    }

    Write-Step 1 2 "Copying to Add-Ins folder..."
    Copy-Item $addinFile $userAddIns -Force
    Write-Ok "File copied to: $userAddIns"

    Write-Step 2 2 "Installation completed"
    Write-Host ""
    Write-Host "To activate the Add-In:" -ForegroundColor Cyan
    Write-Host "  1. Open TIA Portal V21" -ForegroundColor White
    Write-Host "  2. Go to Options > Settings > Add-Ins" -ForegroundColor White
    Write-Host "  3. Activate 'TIA Portal Code Agent'" -ForegroundColor White
    Write-Host ""
    Write-Host "To test: Right-click in project tree > TIA Agent Diagnostics > Test Integration" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================
# Help
# ============================================================

function Show-Help {
    Write-Header "TIA PORTAL CODE AGENT - BUILDER"
    Write-Host "Usage: .\build.ps1 <command>" -ForegroundColor White
    Write-Host ""
    Write-Host "Available commands:" -ForegroundColor Yellow
    Write-Host "  build     Compiles the solution" -ForegroundColor White
    Write-Host "  test      Runs all tests" -ForegroundColor White
    Write-Host "  pack      Generates the .addin OPC package" -ForegroundColor White
    Write-Host "  verify    Verifies the .addin package contents" -ForegroundColor White
    Write-Host "  mcp       Shows MCP server info (Czarnak/tia-portal-mcp)" -ForegroundColor White
    Write-Host "  install   Copies the .addin to TIA Portal folder" -ForegroundColor White
    Write-Host "  clean     Removes build artifacts" -ForegroundColor White
    Write-Host "  all       Build + Test + Pack" -ForegroundColor White
    Write-Host "  help      Shows this help" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 build          # Compile" -ForegroundColor Gray
    Write-Host "  .\build.ps1 all            # Everything (build+test+pack)" -ForegroundColor Gray
    Write-Host "  .\build.ps1 pack           # Generate .addin" -ForegroundColor Gray
    Write-Host "  .\build.ps1 verify         # Check .addin contents" -ForegroundColor Gray
    Write-Host "  .\build.ps1 install        # Install to TIA Portal" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================
# Execution
# ============================================================

switch ($Command) {
    "build"   { Invoke-Build }
    "test"    { Invoke-Test }
    "pack"    { Invoke-Pack }
    "verify"  { Invoke-Verify }
    "mcp"     { Invoke-Mcp }
    "clean"   { Invoke-Clean }
    "install" { Invoke-Install }
    "all"     { Invoke-Build; Invoke-Test; Invoke-Pack }
    "help"    { Show-Help }
    default   { Show-Help }
}
