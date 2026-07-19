#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Portal Code Agent - Simple builder for build, test, and packaging.

.DESCRIPTION
    Generates the correct .addin file for installation in TIA Portal V21.
    Usage: .\build.ps1 [command]

.COMMANDS
    build       - Compiles the solution
    test        - Runs the tests
    pack        - Packages the Add-In (.addin)
    run         - Starts the MCP server with simulator
    all         - Build + Test + Pack
    clean       - Cleans build artifacts
    install     - Copies to TIA Portal Add-Ins folder
    dev         - Starts the development host

.EXAMPLE
    .\build.ps1 build
    .\build.ps1 all
    .\build.ps1 pack
    .\build.ps1 run
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "pack", "run", "all", "clean", "install", "dev", "help")]
    [string]$Command = "help"
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Config = "Release"
$Version = "0.1.0"

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

# ============================================================
# Commands
# ============================================================

function Invoke-Build {
    Write-Header "BUILD"
    Write-Step 1 3 "Compiling solution..."
    
    dotnet build "$Root\TiaAgent.sln" --configuration $Config --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Write-Fail "Build failed"; exit 1 }
    Write-Ok "Solution compiled"
    
    Write-Step 2 3 "Verifying projects..."
    $projects = Get-ChildItem "$Root\src\*\*.csproj" -ErrorAction SilentlyContinue
    Write-Ok "$($projects.Count) source projects found"
    
    $tests = Get-ChildItem "$Root\tests\*\*.csproj" -ErrorAction SilentlyContinue
    Write-Ok "$($tests.Count) test projects found"
    
    Write-Step 3 3 "Verifying artifacts..."
    $dlls = Get-ChildItem "$Root\src\*\bin\$Config\*\*.dll" -Recurse -ErrorAction SilentlyContinue
    Write-Ok "$($dlls.Count) DLLs generated"
    
    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
}

function Invoke-Test {
    Write-Header "TESTS"
    Write-Step 1 2 "Running tests..."
    
    dotnet test "$Root\TiaAgent.sln" --configuration $Config --verbosity normal
    if ($LASTEXITCODE -ne 0) { Write-Fail "Tests failed"; exit 1 }
    
    Write-Step 2 2 "Results"
    Write-Ok "All tests passed"
}

function Invoke-Pack {
    Write-Header "PACKAGING"
    
    $packDir = "$Root\artifacts"
    $addinDir = "$packDir\TiaAgent.AddIn"
    $versionTag = $Version -replace '\.', '-'
    $addinFile = "$packDir\TiaAgent-$versionTag.addin"
    
    # Clean previous packages
    Write-Step 1 6 "Cleaning previous packages..."
    if (Test-Path $addinDir) { Remove-Item $addinDir -Recurse -Force }
    if (Test-Path $addinFile) { Remove-Item $addinFile -Force }
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
    Write-Ok "Directory cleaned"
    
    # Copy Add-In DLLs
    Write-Step 2 6 "Copying Add-In DLLs..."
    $addinBin = "$Root\src\TiaAgent.AddIn\bin\$Config\net48"
    if (Test-Path $addinBin) {
        Copy-Item "$addinBin\*.dll" $addinDir -ErrorAction SilentlyContinue
        Write-Ok "Add-In DLLs copied"
    } else {
        Write-Fail "Add-In build directory not found: $addinBin"
        exit 1
    }
    
    # Copy dependency DLLs
    Write-Step 3 6 "Copying dependencies..."
    $deps = @(
        "TiaAgent.Contracts",
        "TiaAgent.Application",
        "TiaAgent.Simulator",
        "TiaAgent.OpenCode"
    )
    foreach ($dep in $deps) {
        $depBin = "$Root\src\$dep\bin\$Config\netstandard2.0"
        if (Test-Path $depBin) {
            Copy-Item "$depBin\*.dll" $addinDir -ErrorAction SilentlyContinue
        }
    }
    Write-Ok "Dependencies copied"
    
    # Generate Config.xml
    Write-Step 4 6 "Generating Config.xml..."
    $configXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Configuration
    xmlns="http://www.siemens.com/simatic-automation/addin/configuration"
    Version="1.0">
  <Identity>
    <Name>TIA Portal Code Agent</Name>
    <Description>AI-powered engineering assistant for TIA Portal V21</Description>
    <Vendor>TIA Agent Project</Vendor>
    <Version>$Version</Version>
  </Identity>
  <Runtime>
    <TargetPlatform>TIA Portal V21</TargetPlatform>
    <TargetFramework>.NET Framework 4.8</TargetFramework>
    <Architecture>x64</Architecture>
  </Runtime>
  <Permissions>
    <Permission Type="TIA" Level="Restricted" />
    <Permission Type="Security" Level="Restricted" />
  </Permissions>
  <EntryPoint>
    <Assembly>TiaAgent.AddIn.dll</Assembly>
    <Type>TiaAgent.AddIn.Bootstrap</Type>
  </EntryPoint>
</Configuration>
"@
    $configXml | Out-File -FilePath "$addinDir\Config.xml" -Encoding UTF8
    Write-Ok "Config.xml generated"
    
    # Create .addin file (renamed zip)
    Write-Step 5 6 "Creating .addin file..."
    $tempZip = "$packDir\temp.zip"
    Compress-Archive -Path "$addinDir\*" -DestinationPath $tempZip -Force
    Move-Item $tempZip $addinFile -Force
    Write-Ok ".addin file created: $addinFile"
    
    # Summary
    Write-Step 6 6 "Package summary..."
    $size = (Get-Item $addinFile).Length / 1KB
    $files = Get-ChildItem $addinDir -Recurse -File
    Write-Info "File: $addinFile"
    Write-Info "Size: $([math]::Round($size, 1)) KB"
    Write-Info "Version: $Version"
    Write-Info "Files: $($files.Count)"
    
    Write-Host ""
    Write-Host "Packaging completed!" -ForegroundColor Green
    Write-Host "  To install: copy the .addin to %APPDATA%\Siemens\Automation\Portal V21\UserAddIns" -ForegroundColor Gray
}

function Invoke-Run {
    Write-Header "MCP SERVER (SIMULATOR)"
    Write-Info "Starting MCP server at http://127.0.0.1:43121/mcp"
    Write-Info "Press Ctrl+C to stop"
    Write-Host ""
    
    dotnet run --project "$Root\src\TiaAgent.McpHost\TiaAgent.McpHost.csproj" --configuration $Config
}

function Invoke-Dev {
    Write-Header "DEVELOPMENT HOST"
    Write-Info "Starting development console..."
    Write-Host ""
    
    # Create a temporary console project to run the DevelopmentHost
    $tempProject = "$Root\artifacts\TiaAgent.DevHost\TiaAgent.DevHost.csproj"
    $tempDir = Split-Path $tempProject -Parent
    
    if (!(Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }
    
    $projContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$Root\src\TiaAgent.AddIn\TiaAgent.AddIn.csproj" />
    <ProjectReference Include="$Root\src\TiaAgent.Simulator\TiaAgent.Simulator.csproj" />
    <ProjectReference Include="$Root\src\TiaAgent.Application\TiaAgent.Application.csproj" />
    <ProjectReference Include="$Root\src\TiaAgent.Contracts\TiaAgent.Contracts.csproj" />
  </ItemGroup>
</Project>
"@
    $projContent | Out-File -FilePath $tempProject -Encoding UTF8
    
    $programContent = @"
using TiaAgent.AddIn;
using TiaAgent.Application.Common;
using TiaAgent.Application.Hashing;
using TiaAgent.Simulator;

var hashService = new ContentHashService();
var clock = new SystemClock();
var simulator = new SimulatorTiaProjectService(hashService, clock);
var host = new DevelopmentHost(simulator, simulator);
await host.RunAsync();
"@
    $programContent | Out-File -FilePath "$tempDir\Program.cs" -Encoding UTF8
    
    dotnet run --project $tempProject
}

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

function Invoke-Install {
    Write-Header "INSTALL TO TIA PORTAL"
    
    $userAddIns = "$env:APPDATA\Siemens\Automation\Portal V21\UserAddIns\AddIns"
    
    if (!(Test-Path $userAddIns)) {
        Write-Fail "Add-Ins folder not found: $userAddIns"
        Write-Info "Make sure TIA Portal V21 has been run at least once"
        exit 1
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
}

function Show-Help {
    Write-Header "TIA PORTAL CODE AGENT - BUILDER"
    Write-Host "Usage: .\build.ps1 <command>" -ForegroundColor White
    Write-Host ""
    Write-Host "Available commands:" -ForegroundColor Yellow
    Write-Host "  build     Compiles the solution" -ForegroundColor White
    Write-Host "  test      Runs all tests" -ForegroundColor White
    Write-Host "  pack      Generates the .addin file for TIA Portal" -ForegroundColor White
    Write-Host "  run       Starts the MCP server with simulator" -ForegroundColor White
    Write-Host "  dev       Starts the development host (console)" -ForegroundColor White
    Write-Host "  install   Copies the .addin to TIA Portal folder" -ForegroundColor White
    Write-Host "  clean     Removes build artifacts" -ForegroundColor White
    Write-Host "  all       Build + Test + Pack" -ForegroundColor White
    Write-Host "  help      Shows this help" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 build          # Compile" -ForegroundColor Gray
    Write-Host "  .\build.ps1 all            # Everything (build+test+pack)" -ForegroundColor Gray
    Write-Host "  .\build.ps1 pack           # Generate .addin" -ForegroundColor Gray
    Write-Host "  .\build.ps1 run            # Start MCP server" -ForegroundColor Gray
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
    "run"     { Invoke-Run }
    "dev"     { Invoke-Dev }
    "clean"   { Invoke-Clean }
    "install" { Invoke-Install }
    "all"     { Invoke-Build; Invoke-Test; Invoke-Pack }
    "help"    { Show-Help }
    default   { Show-Help }
}
