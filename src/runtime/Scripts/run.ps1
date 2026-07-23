#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Starts and manages all runtime services.

.DESCRIPTION
    Delegates runtime supervision to the tia-agent CLI start command.

.PARAMETER Config
    Path to settings.json (defaults to %LOCALAPPDATA%\TiaAgent\config\settings.json)

.PARAMETER RepoRoot
    Repository root path (auto-detected if not specified)

.PARAMETER NoMonitor
    Start services but do not monitor (exit after health checks pass)

.PARAMETER Verbose
    Enable verbose logging output

.EXAMPLE
    .\run.ps1
    .\run.ps1 -Verbose
    .\run.ps1 -NoMonitor
#>
[CmdletBinding()]
param(
    [string]$Config = '',
    [string]$RepoRoot = '',
    [switch]$NoMonitor
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = $PSScriptRoot

if (-not $RepoRoot) {
    $RepoRoot = (Get-Item $ScriptRoot).Parent.Parent.Parent.FullName
}

$cliExe = Get-Command tia-agent -ErrorAction SilentlyContinue
$cliProject = Join-Path $RepoRoot 'src\TiaAgent.Cli\TiaAgent.Cli.csproj'

$cliArgs = @('start')
if ($Config) { $cliArgs += @('--config', $Config) }
if ($RepoRoot) { $cliArgs += @('--repo-root', $RepoRoot) }
if ($NoMonitor) { $cliArgs += '--no-monitor' }

if ($cliExe) {
    & $cliExe.Source $cliArgs
} elseif (Test-Path $cliProject) {
    dotnet run --project $cliProject -- $cliArgs
} else {
    throw "tia-agent CLI not found. Please install tia-agent or run from repository root."
}
