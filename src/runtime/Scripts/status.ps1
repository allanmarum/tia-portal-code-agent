#Requires -Version 5.1
<#
.SYNOPSIS
    TIA Agent Runtime Supervisor - Shows runtime status and health information.

.DESCRIPTION
    Delegates status query to the tia-agent CLI status command.

.PARAMETER TiaAgentRoot
    TiaAgent root directory (defaults to %LOCALAPPDATA%\TiaAgent)

.PARAMETER Json
    Output in machine-readable JSON format

.EXAMPLE
    .\status.ps1
    .\status.ps1 -Json
#>
[CmdletBinding()]
param(
    [string]$TiaAgentRoot = (Join-Path $env:LOCALAPPDATA 'TiaAgent'),
    [switch]$Json
)

$ErrorActionPreference = 'SilentlyContinue'
$ScriptRoot = $PSScriptRoot
$RepoRoot = (Get-Item $ScriptRoot).Parent.Parent.Parent.FullName

$cliExe = Get-Command tia-agent -ErrorAction SilentlyContinue
$cliProject = Join-Path $RepoRoot 'src\TiaAgent.Cli\TiaAgent.Cli.csproj'

$cliArgs = @('status')
if ($TiaAgentRoot) { $cliArgs += @('--custom-root', $TiaAgentRoot) }
if ($Json) { $cliArgs += '--json' }

if ($cliExe) {
    & $cliExe.Source $cliArgs
} elseif (Test-Path $cliProject) {
    dotnet run --project $cliProject -- $cliArgs
} else {
    throw "tia-agent CLI not found. Please install tia-agent or run from repository root."
}
