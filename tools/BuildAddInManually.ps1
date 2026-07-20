#Requires -Version 5.1
<#
.SYNOPSIS
    Creates a TIA Portal .addin package manually (bypassing Publisher cache issues).
#>
param(
    [string]$OutputPath = "C:\github\tia-portal-code-agent\artifacts\TiaAgent-0-1-0.addin"
)

$ErrorActionPreference = "Stop"
$root = "C:\github\tia-portal-code-agent"
$buildDir = "$root\src\TiaAgent.AddIn\bin\Release\net48"
$simDir = "$root\src\TiaAgent.Simulator\bin\Release\netstandard2.0"

Write-Host "`n=== Building .addin manually ===" -ForegroundColor Cyan

Add-Type -AssemblyName WindowsBase

# Create package
$pkg = [System.IO.Packaging.Package]::Open($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite)

# --- Metadata ---
function Add-Part($uri, $content) {
    $part = $pkg.CreatePart($uri, "text/plain")
    $writer = New-Object System.IO.StreamWriter($part.GetStream())
    $writer.Write($content)
    $writer.Close()
}

function Add-BinaryPart($uri, $filePath) {
    $bytes = [System.IO.File]::ReadAllBytes($filePath)
    $part = $pkg.CreatePart($uri, "application/octet-stream")
    $stream = $part.GetStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    return $bytes.Length
}

# Engineering version
Add-Part "/EngineeringVersion" "21"

# Product info
Add-Part "/Product/Id" "tia-portal-code-agent"
Add-Part "/Product/ProductName" "TIA Portal Code Agent"

# Multiuser
Add-Part "/Multiuser/DisplayState" "false"

# DevTools
Add-Part "/DevToolsInfo/ProjectTemplate" "false"

# Permissions
Add-Part "/Permissions/Required/Tia/TIA.ReadOnly" "TIA.ReadOnly"
Add-Part "/Permissions/Required/Security/System.UnrestrictedAccess" "System.UnrestrictedAccess`nFull trust for network and UI access"

# Meta
$timestamp = [DateTime]::UtcNow.ToString("o")
Add-Part "/Meta/TimeStampUTC" $timestamp
Add-Part "/Meta/Version" '{"SchemaVersion":"2.0","AddInVersion":"0.1.0","PublisherVersion":"1.0"}'
Add-Part "/Meta/Description" "AI-powered engineering assistant for TIA Portal V21"
Add-Part "/Meta/Location" "Local"
Add-Part "/Meta/PublisherTarget" "UserAddIns"

# --- Assemblies ---
$assemblies = @(
    @{ Uri = "/TIAAGENT.ADDIN,%20VERSION=0.1.0.0,%20CULTURE=NEUTRAL,%20PUBLICKEYTOKEN=NULL,%20PROCESSORARCHITECTURE=MSIL"; File = "$buildDir\TiaAgent.AddIn.dll" },
    @{ Uri = "/LocalAssemblyCache/TIAAGENT.CONTRACTS,%20VERSION=0.1.0.0,%20CULTURE=NEUTRAL,%20PUBLICKEYTOKEN=NULL,%20PROCESSORARCHITECTURE=MSIL"; File = "$buildDir\TiaAgent.Contracts.dll" },
    @{ Uri = "/LocalAssemblyCache/TIAAGENT.APPLICATION,%20VERSION=0.1.0.0,%20CULTURE=NEUTRAL,%20PUBLICKEYTOKEN=NULL,%20PROCESSORARCHITECTURE=MSIL"; File = "$buildDir\TiaAgent.Application.dll" },
    @{ Uri = "/LocalAssemblyCache/TIAAGENT.SIMULATOR,%20VERSION=0.1.0.0,%20CULTURE=NEUTRAL,%20PUBLICKEYTOKEN=NULL,%20PROCESSORARCHITECTURE=MSIL"; File = "$simDir\TiaAgent.Simulator.dll" },
    @{ Uri = "/LocalAssemblyCache/TIAAGENT.OPENCODE,%20VERSION=0.1.0.0,%20CULTURE=NEUTRAL,%20PUBLICKEYTOKEN=NULL,%20PROCESSORARCHITECTURE=MSIL"; File = "$buildDir\TiaAgent.OpenCode.dll" }
)

# Transitive dependencies
$deps = @(
    "System.Text.Json.dll", "System.Text.Encodings.Web.dll",
    "System.Buffers.dll", "System.Memory.dll",
    "System.Runtime.CompilerServices.Unsafe.dll", "Microsoft.Bcl.AsyncInterfaces.dll",
    "System.Threading.Tasks.Extensions.dll", "System.Numerics.Vectors.dll",
    "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
    "Microsoft.Extensions.Logging.Abstractions.dll"
)
foreach ($dep in $deps) {
    $depPath = "$buildDir\$dep"
    if (Test-Path $depPath) {
        # Get assembly version
        $asmName = [System.Reflection.AssemblyName]::GetAssemblyName($depPath)
        $name = $asmName.Name.ToUpper()
        $ver = $asmName.Version.ToString()
        $assemblies += @{
            Uri = "/LocalAssemblyCache/$name,%20VERSION=$ver,%20CULTURE=NEUTRAL,%20PUBLICKEYTOKEN=CC7B13FFCD2DDD51,%20PROCESSORARCHITECTURE=MSIL"
            File = $depPath
        }
    }
}

$addedCount = 0
foreach ($asm in $assemblies) {
    $size = Add-BinaryPart $asm.Uri $asm.File
    $name = ($asm.Uri -replace "/.*/", "" -replace ",.*", "")
    Write-Host "  $name ($size bytes)" -ForegroundColor Gray
    $addedCount++
}
Write-Host "Added $addedCount assemblies" -ForegroundColor Green

$pkg.Close()

# --- Sign ---
$signer = "$root\tools\OpcSigner\bin\Release\net48\OpcSigner.exe"
Write-Host "`nSigning..." -ForegroundColor Yellow
& $signer $OutputPath 2>&1

# --- Deploy ---
$deploy = "$env:APPDATA\Siemens\Automation\Portal V21\UserAddIns\TiaAgent-0-1-0.addin"
Copy-Item $OutputPath $deploy -Force

# Verify
$pkg2 = [System.IO.Packaging.Package]::Open($deploy, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
foreach ($part in $pkg2.GetParts()) {
    if ($part.Uri.ToString() -match "TIAAGENT\.ADDIN") {
        $stream = $part.GetStream()
        $bytes = New-Object byte[] $stream.Length
        $stream.Read($bytes, 0, $stream.Length)
        $stream.Close()
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        Write-Host "`nVerify: $($bytes.Length) bytes, MB:$($text.Contains('MessageBox')) HA:$($text.Contains('HandleAction'))" -ForegroundColor $(if ($text.Contains('MessageBox')) {'Green'} else {'Red'})
        break
    }
}
$pkg2.Close()

Write-Host "Deployed: $deploy" -ForegroundColor Green
