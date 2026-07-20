# TiaAgent.Supervisor module - loads all function files
$FunctionPath = Join-Path $PSScriptRoot 'Functions'
Get-ChildItem -Path $FunctionPath -Filter '*.ps1' -ErrorAction SilentlyContinue | ForEach-Object {
    . $_.FullName
}
