#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies the development environment for TIA Portal Code Agent.

.DESCRIPTION
    Checks .NET SDK, TIA Portal installation, Openness assemblies, port availability,
    and configuration files. Produces a readable pass/warn/fail report.

.EXAMPLE
    .\scripts\verify-environment.ps1
#>

$ErrorActionPreference = "Continue"

function Write-Status($Status, $Message) {
    switch ($Status) {
        "PASS" { Write-Host "  [PASS] " -ForegroundColor Green -NoNewline; Write-Host $Message }
        "WARN" { Write-Host "  [WARN] " -ForegroundColor Yellow -NoNewline; Write-Host $Message }
        "FAIL" { Write-Host "  [FAIL] " -ForegroundColor Red -NoNewline; Write-Host $Message }
    }
}

Write-Host "`n=== TIA Portal Code Agent - Environment Verification ===" -ForegroundColor Cyan
Write-Host ""

# 1. .NET SDK
Write-Host "--- .NET SDK ---" -ForegroundColor Yellow
try {
    $sdkVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Status "PASS" ".NET SDK version: $sdkVersion"
    } else {
        Write-Status "FAIL" ".NET SDK not found or not working"
    }
} catch {
    Write-Status "FAIL" ".NET SDK not found"
}

try {
    $sdks = dotnet --list-sdks 2>&1
    if ($sdks -match "8\.0") {
        Write-Status "PASS" ".NET 8 SDK available"
    } else {
        Write-Status "WARN" ".NET 8 SDK not found (required for MCP server)"
    }
} catch {
    Write-Status "WARN" "Could not list SDKs"
}

# 2. .NET Framework 4.8
Write-Host "`n--- .NET Framework 4.8 ---" -ForegroundColor Yellow
$fwKey = "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
if (Test-Path $fwKey) {
    $release = (Get-ItemProperty $fwKey -Name Release -ErrorAction SilentlyContinue).Release
    if ($release -ge 528040) {
        Write-Status "PASS" ".NET Framework 4.8 or higher installed (Release: $release)"
    } else {
        Write-Status "WARN" ".NET Framework installed but release $release < 528040"
    }
} else {
    Write-Status "FAIL" ".NET Framework 4.8 not found"
}

# 3. TIA Portal
Write-Host "`n--- TIA Portal V21 ---" -ForegroundColor Yellow
$tiaPath = "C:\Program Files\Siemens\Automation\Portal V21"
if (Test-Path $tiaPath) {
    Write-Status "PASS" "TIA Portal V21 installed at $tiaPath"
} else {
    Write-Status "WARN" "TIA Portal V21 not found at default path"
}

# 4. Openness Assemblies
Write-Host "`n--- Openness Assemblies ---" -ForegroundColor Yellow
$publicApiDir = "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48"
if (Test-Path $publicApiDir) {
    $dlls = Get-ChildItem "$publicApiDir\Siemens.Engineering*.dll" -ErrorAction SilentlyContinue
    if ($dlls.Count -gt 0) {
        Write-Status "PASS" "Found $($dlls.Count) Siemens assemblies in PublicAPI"
    } else {
        Write-Status "WARN" "PublicAPI directory exists but no Siemens DLLs found"
    }
} else {
    Write-Status "WARN" "PublicAPI directory not found"
}

# 5. User Add-Ins Folder
Write-Host "`n--- User Add-Ins Folder ---" -ForegroundColor Yellow
$userAddIns = "$env:APPDATA\Siemens\Automation\Portal V21\UserAddIns"
if (Test-Path $userAddIns) {
    Write-Status "PASS" "User Add-Ins folder exists"
} else {
    Write-Status "WARN" "User Add-Ins folder not found (TIA Portal may need to be run once)"
}

# 6. Port Availability
Write-Host "`n--- Port Availability ---" -ForegroundColor Yellow
$ports = @(43121, 43120)
foreach ($port in $ports) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($connection) {
        Write-Status "WARN" "Port $port is in use"
    } else {
        Write-Status "PASS" "Port $port is available"
    }
}

# 7. Configuration Files
Write-Host "`n--- Configuration Files ---" -ForegroundColor Yellow
$examples = @(
    "config\appsettings.example.json",
    "config\opencode.example.json",
    "config\capabilities.example.json"
)
foreach ($ex in $examples) {
    if (Test-Path $ex) {
        Write-Status "PASS" "$ex exists"
    } else {
        Write-Status "WARN" "$ex not found"
    }
}

# 8. Solution File
Write-Host "`n--- Solution Structure ---" -ForegroundColor Yellow
if (Test-Path "TiaAgent.sln") {
    Write-Status "PASS" "TiaAgent.sln found"
} else {
    Write-Status "FAIL" "TiaAgent.sln not found"
}

$srcProjects = Get-ChildItem "src\*\*.csproj" -ErrorAction SilentlyContinue
if ($srcProjects.Count -gt 0) {
    Write-Status "PASS" "Found $($srcProjects.Count) source projects"
} else {
    Write-Status "WARN" "No source projects found"
}

$testProjects = Get-ChildItem "tests\*\*.csproj" -ErrorAction SilentlyContinue
if ($testProjects.Count -gt 0) {
    Write-Status "PASS" "Found $($testProjects.Count) test projects"
} else {
    Write-Status "WARN" "No test projects found"
}

# 9. Build Test
Write-Host "`n--- Build Verification ---" -ForegroundColor Yellow
Write-Host "  Running dotnet build..." -ForegroundColor Gray
$buildOutput = dotnet build TiaAgent.sln --verbosity quiet 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Status "PASS" "Solution builds successfully"
} else {
    Write-Status "FAIL" "Solution build failed"
}

Write-Host "`n=== Verification Complete ===" -ForegroundColor Cyan
Write-Host ""
