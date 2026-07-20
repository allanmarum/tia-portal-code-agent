#Requires -Version 5.1
<#
.SYNOPSIS
    Processes pending MCP requests from the TIA Portal Add-In.
    Runs outside the sandbox with full permissions.
#>
param(
    [string]$McpToken = "jfd+2bkdjgfOsMZNzGM4uPiQUot1t0HpWu4yGXJNO8w=",
    [string]$McpUrl = "http://127.0.0.1:43121/mcp"
)

$ErrorActionPreference = "Stop"
$logDir = "$env:LOCALAPPDATA\TiaAgent"
$requestFile = "$logDir\pending_request.json"
$resultFile = "$logDir\last_result.txt"

Write-Host "`n=== TIA Agent MCP Processor ===" -ForegroundColor Cyan

if (-not (Test-Path $requestFile)) {
    Write-Host "No pending request. Select an object in TIA Portal first." -ForegroundColor Yellow
    exit 0
}

$request = Get-Content $requestFile -Raw | ConvertFrom-Json
Write-Host "Processing: $($request.action) on $($request.object) ($($request.type))" -ForegroundColor Gray

# Call MCP server
Write-Host "`nCalling MCP server..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $McpToken"
    "Accept" = "application/json, text/event-stream"
}

try {
    # Initialize session
    $initBody = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"tia-addin","version":"0.1.0"}}}'
    $initResp = Invoke-RestMethod -Uri $McpUrl -Method POST -ContentType "application/json" -Body $initBody -Headers $headers
    $sessionId = $initResp.id

    # Get context
    $contextBody = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"tia_get_current_context","arguments":{}}}'
    $contextResp = Invoke-RestMethod -Uri $McpUrl -Method POST -ContentType "application/json" -Body $contextBody -Headers $headers

    $result = "MCP Response for: $($request.action) on $($request.object)`n`n"
    $result += ($contextResp | ConvertTo-Json -Depth 10)

    # Save result
    $result | Out-File $resultFile -Encoding UTF8
    Write-Host "`nResult saved to: $resultFile" -ForegroundColor Green
    Write-Host $result -ForegroundColor White

    # Remove pending request
    Remove-Item $requestFile -Force
} catch {
    $errorMsg = "MCP Error: $($_.Exception.Message)"
    $errorMsg | Out-File $resultFile -Encoding UTF8
    Write-Host $errorMsg -ForegroundColor Red
}

Write-Host "`nDone." -ForegroundColor Green
