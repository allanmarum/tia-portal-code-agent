#Requires -Version 5.1
<#
.SYNOPSIS
    Starts the simulator and MCP server for development.

.DESCRIPTION
    Launches the McpHost console application with the simulator adapter,
    making all TIA tools available via HTTP on localhost.
#>

$root = Split-Path $PSScriptRoot -Parent

Write-Host "`n=== TIA Portal Code Agent - Simulator ===" -ForegroundColor Cyan
Write-Host "Starting MCP server on http://127.0.0.1:43121/mcp" -ForegroundColor Gray
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host ""

dotnet run --project "$root\src\TiaAgent.McpHost\TiaAgent.McpHost.csproj" --configuration Release
