#Requires -Version 5.1
<#
.SYNOPSIS
    Runs tests that do not require an active TIA Portal instance.
#>
param(
    [string]$Filter = ""
)

$root = Split-Path $PSScriptRoot -Parent

Write-Host "`n=== TIA Portal Code Agent - Test ===" -ForegroundColor Cyan

$testArgs = @(
    "test"
    "$root\TiaAgent.sln"
    "--configuration", "Release"
    "--verbosity", "normal"
)

if ($Filter) {
    $testArgs += "--filter", $Filter
}

Write-Host "Running tests..." -ForegroundColor Yellow
& dotnet @testArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nTests FAILED" -ForegroundColor Red
    exit 1
}

Write-Host "`nAll tests passed!" -ForegroundColor Green
