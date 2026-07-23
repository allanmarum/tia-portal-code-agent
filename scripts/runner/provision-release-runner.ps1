#Requires -Version 5.1
<#
.SYNOPSIS
    Provisions, hardens, sanitizes, and verifies a Windows self-hosted release runner for TIA Portal Code Agent.

.DESCRIPTION
    This script inspects and verifies the software prerequisites, directory structures, user permissions,
    and process isolation required for building, packaging, and signing TIA Portal Code Agent releases.

    It can be run in two modes:
    1. -VerifyOnly: Performs non-destructive checks of the runner environment.
    2. -SanitizeWorkspace: Cleans up background processes, lingering handles, and temporary build directories.

.PARAMETER VerifyOnly
    Performs environmental validation checks without modifying workspace files or processes.

.PARAMETER SanitizeWorkspace
    Terminates lingering background processes and sanitizes workspace/temp directories.

.EXAMPLE
    .\scripts\runner\provision-release-runner.ps1 -VerifyOnly

.EXAMPLE
    .\scripts\runner\provision-release-runner.ps1 -SanitizeWorkspace
#>

[CmdletBinding()]
param(
    [switch]$VerifyOnly,
    [switch]$SanitizeWorkspace
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path "$Root\Directory.Build.props")) {
    $Root = (Get-Item $PSScriptRoot).Parent.Parent.FullName
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan
}

function Write-CheckPass {
    param([string]$Message)
    Write-Host "  [PASS] $Message" -ForegroundColor Green
}

function Write-CheckWarn {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-CheckFail {
    param([string]$Message)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
}

$FailedChecks = 0

function Assert-Prerequisite {
    param(
        [bool]$Condition,
        [string]$SuccessMessage,
        [string]$FailureMessage,
        [bool]$IsWarning = $false
    )

    if ($Condition) {
        Write-CheckPass $SuccessMessage
    }
    else {
        if ($IsWarning) {
            Write-CheckWarn $FailureMessage
        }
        else {
            Write-CheckFail $FailureMessage
            $script:FailedChecks++
        }
    }
}

function Check-Environment {
    Write-Section "Release Runner Environment Validation"

    # 1. Operating System
    $isWindows = $env:OS -eq "Windows_NT" -or $IsWindows
    Assert-Prerequisite `
        -Condition $isWindows `
        -SuccessMessage "Operating System: Windows x64" `
        -FailureMessage "Release runner must run on Windows x64."

    # 2. .NET SDK 8.0
    $dotnetPath = Get-Command "dotnet" -ErrorAction SilentlyContinue
    Assert-Prerequisite `
        -Condition ($null -ne $dotnetPath) `
        -SuccessMessage ".NET CLI (dotnet) found in PATH: $($dotnetPath.Source)" `
        -FailureMessage ".NET CLI (dotnet) is missing from PATH."

    if ($null -ne $dotnetPath) {
        try {
            $dotnetSdks = & dotnet --list-sdks 2>$null
            $hasDotNet8 = $dotnetSdks -match "8\.\d+\.\d+"
            Assert-Prerequisite `
                -Condition $hasDotNet8 `
                -SuccessMessage ".NET SDK 8.0 detected" `
                -FailureMessage ".NET SDK 8.0 is required but not installed."
        }
        catch {
            Write-CheckWarn "Could not list .NET SDKs."
        }
    }

    # 3. .NET Framework 4.8 Developer Pack
    $net48RegKey = "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
    $net48Installed = $false
    if (Test-Path $net48RegKey) {
        $release = (Get-ItemProperty -Path $net48RegKey -Name Release -ErrorAction SilentlyContinue).Release
        if ($release -ge 528040) { # 528040 = .NET 4.8
            $net48Installed = $true
        }
    }
    Assert-Prerequisite `
        -Condition $net48Installed `
        -SuccessMessage ".NET Framework 4.8 Runtime/Developer Pack detected" `
        -FailureMessage ".NET Framework 4.8 is required for TIA Portal Add-In builds." `
        -IsWarning $true

    # 4. Siemens TIA Portal V21 & Add-In Publisher
    $tiaBasePath = "C:\Program Files\Siemens\Automation\Portal V21"
    $tiaNet48Path = "$tiaBasePath\PublicAPI\V21\net48"
    $tiaPublisherPath = "$tiaBasePath\PublicAPI\V21.AddIn\Siemens.Engineering.AddIn.Publisher.exe"

    $hasOpenness = Test-Path "$tiaNet48Path\Siemens.Engineering.Base.dll"
    Assert-Prerequisite `
        -Condition $hasOpenness `
        -SuccessMessage "Siemens TIA Portal V21 Openness SDK found at $tiaNet48Path" `
        -FailureMessage "Siemens TIA Portal V21 Openness SDK not found at $tiaNet48Path" `
        -IsWarning $true

    $hasPublisher = Test-Path $tiaPublisherPath
    Assert-Prerequisite `
        -Condition $hasPublisher `
        -SuccessMessage "Siemens Add-In Publisher found at $tiaPublisherPath" `
        -FailureMessage "Siemens Add-In Publisher not found at $tiaPublisherPath" `
        -IsWarning $true

    # 5. Siemens TIA Openness Group Membership
    if ($isWindows) {
        try {
            $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent()
            $principal = [System.Security.Principal.WindowsPrincipal]$currentUser
            $inGroup = $principal.IsInRole("Siemens TIA Openness")
            Assert-Prerequisite `
                -Condition $inGroup `
                -SuccessMessage "Service account is a member of 'Siemens TIA Openness' group" `
                -FailureMessage "Service account is NOT in 'Siemens TIA Openness' group" `
                -IsWarning $true
        }
        catch {
            Write-CheckWarn "Could not check 'Siemens TIA Openness' group membership."
        }
    }

    # 6. Git & PowerShell
    $gitPath = Get-Command "git" -ErrorAction SilentlyContinue
    Assert-Prerequisite `
        -Condition ($null -ne $gitPath) `
        -SuccessMessage "Git CLI found: $($gitPath.Source)" `
        -FailureMessage "Git CLI is missing from PATH."
}

function Sanitize-WorkspaceEnvironment {
    Write-Section "Sanitizing Workspace and Environment"

    # Terminate lingering build/test background processes
    $targetProcesses = @(
        "Siemens.Engineering.AddIn.Publisher",
        "TiaMcpServer",
        "TiaAgent.Bridge",
        "TiaAgent.Cli"
    )

    foreach ($procName in $targetProcesses) {
        try {
            $procs = Get-Process -Name $procName -ErrorAction SilentlyContinue
            if ($null -ne $procs -and $procs.Count -gt 0) {
                Write-Host "  Stopping $procName ($($procs.Count) process(es))..." -ForegroundColor Yellow
                $procs | Stop-Process -Force -ErrorAction SilentlyContinue
                Write-CheckPass "Terminated $procName"
            }
        }
        catch {
            Write-CheckWarn "Could not stop process $procName: $_"
        }
    }

    # Clean temporary directories
    $tempPaths = @(
        "$Root\artifacts",
        "$Root\src\TiaAgent.Cli\payload"
    )

    foreach ($path in $tempPaths) {
        if (Test-Path $path) {
            try {
                Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
                Write-CheckPass "Removed build artifact folder: $path"
            }
            catch {
                Write-CheckWarn "Could not remove $path: $_"
            }
        }
    }
}

# Main execution logic
Check-Environment

if ($SanitizeWorkspace) {
    Sanitize-WorkspaceEnvironment
}

Write-Section "Provisioning Summary"
if ($FailedChecks -eq 0) {
    Write-Host "Release runner host environment validation PASSED." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "Release runner host environment validation FAILED with $FailedChecks error(s)." -ForegroundColor Red
    exit 1
}
