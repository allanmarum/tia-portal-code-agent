#Requires -Version 5.1
<#
.SYNOPSIS
    Starts the MCP server for the selected profile.
#>
param(
    [ValidateSet("Simulator", "LocalTIA")]
    [string]$Profile = "Simulator"
)

$root = Split-Path $PSScriptRoot -Parent

Write-Host "`n=== TIA Portal Code Agent - MCP Server ===" -ForegroundColor Cyan
Write-Host "Profile: $Profile" -ForegroundColor Gray

switch ($Profile) {
    "Simulator" {
        Write-Host "Starting with simulator adapter..." -ForegroundColor Yellow
        & "$root\scripts\run-simulator.ps1"
    }
    "LocalTIA" {
        Write-Host "Starting with TIA Portal adapter..." -ForegroundColor Yellow
        Write-Host "WARNING: Requires TIA Portal V21 to be running" -ForegroundColor Red
        dotnet run --project "$root\src\TiaAgent.McpHost\TiaAgent.McpHost.csproj" --configuration Release
    }
}
