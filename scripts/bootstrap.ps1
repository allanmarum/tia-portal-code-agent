#Requires -Version 5.1
<#
.SYNOPSIS
    Bootstraps the local development environment.

.DESCRIPTION
    Restores packages, creates local config from examples, builds the solution,
    and generates development secrets.
#>

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "`n=== TIA Portal Code Agent - Bootstrap ===" -ForegroundColor Cyan

# 1. Restore packages
Write-Host "`n[1/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore "$root\TiaAgent.sln"
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

# 2. Create local config from examples
Write-Host "`n[2/5] Creating local configuration files..." -ForegroundColor Yellow
$configDir = "$root\config"
$localConfigs = @(
    @{ Source = "appsettings.example.json"; Target = "appsettings.json" },
    @{ Source = "opencode.example.json"; Target = "opencode.json" }
)
foreach ($cfg in $localConfigs) {
    $sourcePath = Join-Path $configDir $cfg.Source
    $targetPath = Join-Path $configDir $cfg.Target
    if (!(Test-Path $targetPath) -and (Test-Path $sourcePath)) {
        Copy-Item $sourcePath $targetPath
        Write-Host "  Created $($cfg.Target) from example" -ForegroundColor Gray
    } else {
        Write-Host "  $($cfg.Target) already exists, skipping" -ForegroundColor Gray
    }
}

# 3. Build solution
Write-Host "`n[3/5] Building solution..." -ForegroundColor Yellow
dotnet build "$root\TiaAgent.sln" --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 4. Run tests
Write-Host "`n[4/5] Running tests..." -ForegroundColor Yellow
dotnet test "$root\TiaAgent.sln" --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

# 5. Summary
Write-Host "`n[5/5] Bootstrap complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run simulator: .\scripts\run-simulator.ps1" -ForegroundColor White
Write-Host "  2. Start MCP:     .\scripts\run-mcp.ps1" -ForegroundColor White
Write-Host "  3. Run tests:     .\scripts\test.ps1" -ForegroundColor White
Write-Host ""
