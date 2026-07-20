#Requires -Version 5.1
<#
.SYNOPSIS
    Adds assembly binding redirects for TIA Portal Code Agent dependencies.
    Must be run as Administrator.
#>

$ErrorActionPreference = "Stop"

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator." -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

$configPath = "C:\Program Files\Siemens\Automation\Portal V21\Bin\Siemens.Automation.Portal.exe.config"

if (-not (Test-Path $configPath)) {
    Write-Host "ERROR: TIA Portal config not found at: $configPath" -ForegroundColor Red
    exit 1
}

Write-Host "=== Adding Binding Redirects to TIA Portal Config ===" -ForegroundColor Cyan
Write-Host "Target: $configPath" -ForegroundColor Gray

# Backup
$backupPath = "$configPath.pre-tiaagent"
if (-not (Test-Path $backupPath)) {
    Copy-Item $configPath $backupPath -Force
    Write-Host "Backup created: $backupPath" -ForegroundColor Gray
}

$content = Get-Content $configPath -Raw

# Check if already patched
if ($content -match "System\.Text\.Json.*bindingRedirect") {
    Write-Host "Binding redirects already present, skipping." -ForegroundColor Yellow
    exit 0
}

# New binding redirects to add
$newRedirects = @'
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Text.Json" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-8.0.0.5" newVersion="8.0.0.5" />
      </dependentAssembly>
    </assemblyBinding>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Text.Encodings.Web" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-8.0.0.0" newVersion="8.0.0.0" />
      </dependentAssembly>
    </assemblyBinding>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="Microsoft.Bcl.AsyncInterfaces" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-8.0.0.0" newVersion="8.0.0.0" />
      </dependentAssembly>
    </assemblyBinding>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Threading.Tasks.Extensions" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.2.0.1" newVersion="4.2.0.1" />
      </dependentAssembly>
    </assemblyBinding>
'@

# Insert before closing </runtime>
$content = $content -replace '(?s)(  </runtime>)', "$newRedirects`n  `$1"

# Write
[System.IO.File]::WriteAllText($configPath, $content, [System.Text.Encoding]::UTF8)
Write-Host "Config updated successfully!" -ForegroundColor Green

# Verify
$updated = Get-Content $configPath -Raw
if ($updated -match "System\.Text\.Json.*bindingRedirect") {
    Write-Host "Verified: System.Text.Json binding redirect present" -ForegroundColor Green
} else {
    Write-Host "WARNING: Could not verify binding redirect" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Restart TIA Portal to apply changes." -ForegroundColor Cyan
