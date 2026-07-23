#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Gracefully stops all runtime services.

.DESCRIPTION
    Delegates runtime shutdown to the tia-agent CLI stop command.

.PARAMETER TiaAgentRoot
    TiaAgent root directory (defaults to %LOCALAPPDATA%\TiaAgent)

.PARAMETER Force
    Force kill processes without waiting for graceful shutdown

.EXAMPLE
    .\stop.ps1
    .\stop.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = $PSScriptRoot
$RepoRoot = (Get-Item $ScriptRoot).Parent.Parent.Parent.FullName

$cliExe = Get-Command tia-agent -ErrorAction SilentlyContinue
$cliProject = Join-Path $RepoRoot 'src\TiaAgent.Cli\TiaAgent.Cli.csproj'

$cliArgs = @('stop')
if ($TiaAgentRoot) { $cliArgs += @('--custom-root', $TiaAgentRoot) }
if ($Force) { $cliArgs += '--force' }

if ($cliExe) {
    & $cliExe.Source $cliArgs
} elseif (Test-Path $cliProject) {
    dotnet run --project $cliProject -- $cliArgs
} else {
    throw "tia-agent CLI not found. Please install tia-agent or run from repository root."
}
