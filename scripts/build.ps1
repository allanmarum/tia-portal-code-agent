#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the solution for the selected profile.

.PARAMETER Configuration
    Build configuration: Debug or Release. Default: Release.

.PARAMETER Profile
    Build profile: Simulator, LocalTIA, or All. Default: Simulator.
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [ValidateSet("Simulator", "LocalTIA", "All")]
    [string]$Profile = "Simulator"
)

$root = Split-Path $PSScriptRoot -Parent

Write-Host "`n=== TIA Portal Code Agent - Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Profile: $Profile" -ForegroundColor Gray

switch ($Profile) {
    "Simulator" {
        Write-Host "`nBuilding simulator-compatible projects..." -ForegroundColor Yellow
        dotnet build "$root\TiaAgent.sln" --configuration $Configuration
    }
    "LocalTIA" {
        Write-Host "`nBuilding all projects including TIA integration..." -ForegroundColor Yellow
        dotnet build "$root\TiaAgent.sln" --configuration $Configuration
    }
    "All" {
        Write-Host "`nBuilding all projects..." -ForegroundColor Yellow
        dotnet build "$root\TiaAgent.sln" --configuration $Configuration
    }
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild FAILED" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild succeeded!" -ForegroundColor Green
