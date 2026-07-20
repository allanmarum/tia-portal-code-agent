#Requires -Version 5.1
<#
.SYNOPSIS
    Injects missing transitive dependencies into the .addin package
    and redeploys to TIA Portal UserAddIns directory.

.DESCRIPTION
    Fixes the System.Text.Json FileNotFoundException by ensuring all
    NuGet transitive dependencies are included in the OPC package.
#>
param(
    [string]$AddinPath,
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent

# ============================================================
# Find or build the .addin package
# ============================================================

if (-not $AddinPath) {
    $versionTag = "0-1-0"
    $candidates = @(
        "$Root\artifacts\TiaAgent-$versionTag.addin",
        "$Root\artifacts\TiaAgent-mixed.addin"
    )
    $AddinPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $AddinPath -or !(Test-Path $AddinPath)) {
    Write-Host "ERROR: .addin file not found. Run setup.ps1 or build.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Fix Add-In Dependencies ===" -ForegroundColor Cyan
Write-Host "Package: $AddinPath" -ForegroundColor Gray

# ============================================================
# Step 1: Build if needed
# ============================================================

Write-Host "`n[1/4] Ensuring build is up to date..." -ForegroundColor Yellow
dotnet build "$Root\TiaAgent.sln" --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  Build succeeded" -ForegroundColor Green

# ============================================================
# Step 2: Define assemblies to inject
# ============================================================

Write-Host "`n[2/4] Identifying dependencies to inject..." -ForegroundColor Yellow

# All transitive dependencies needed by TiaAgent.OpenCode -> System.Text.Json
$dependencies = @(
    @{ Name = "System.Text.Json"; Version = "8.0.5"; Path = "system.text.json\8.0.5\lib\netstandard2.0\System.Text.Json.dll" },
    @{ Name = "System.Text.Encodings.Web"; Version = "8.0.0"; Path = "system.text.encodings.web\8.0.0\lib\netstandard2.0\System.Text.Encodings.Web.dll" },
    @{ Name = "System.Buffers"; Version = "4.5.1"; Path = "system.buffers\4.5.1\lib\netstandard2.0\System.Buffers.dll" },
    @{ Name = "System.Memory"; Version = "4.5.5"; Path = "system.memory\4.5.5\lib\netstandard2.0\System.Memory.dll" },
    @{ Name = "System.Runtime.CompilerServices.Unsafe"; Version = "6.0.0"; Path = "system.runtime.compilerservices.unsafe\6.0.0\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll" },
    @{ Name = "Microsoft.Bcl.AsyncInterfaces"; Version = "8.0.0"; Path = "microsoft.bcl.asyncinterfaces\8.0.0\lib\netstandard2.0\Microsoft.Bcl.AsyncInterfaces.dll" },
    @{ Name = "System.Threading.Tasks.Extensions"; Version = "4.5.4"; Path = "system.threading.tasks.extensions\4.5.4\lib\netstandard2.0\System.Threading.Tasks.Extensions.dll" },
    @{ Name = "System.Numerics.Vectors"; Version = "4.5.0"; Path = "system.numerics.vectors\4.5.0\lib\netstandard2.0\System.Numerics.Vectors.dll" },
    @{ Name = "Microsoft.Extensions.DependencyInjection.Abstractions"; Version = "8.0.3"; Path = "microsoft.extensions.dependencyinjection.abstractions\8.0.3\lib\netstandard2.0\Microsoft.Extensions.DependencyInjection.Abstractions.dll" },
    @{ Name = "Microsoft.Extensions.Logging.Abstractions"; Version = "8.0.3"; Path = "microsoft.extensions.logging.abstractions\8.0.3\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll" }
)

$nugetCache = "$env:USERPROFILE\.nuget\packages"
$buildOutput = "$Root\src\TiaAgent.AddIn\bin\Release\net48"

# Resolve actual DLL paths (prefer build output, fallback to NuGet cache)
$resolvedDeps = @()
foreach ($dep in $dependencies) {
    $dllPath = "$buildOutput\$($dep.Name).dll"
    if (-not (Test-Path $dllPath)) {
        $dllPath = "$nugetCache\$($dep.Path)"
    }
    if (Test-Path $dllPath) {
        $resolvedDeps += @{
            Name = $dep.Name
            Version = $dep.Version
            DllPath = $dllPath
            OpcEntryName = "$($dep.Name.ToUpper()), VERSION=$($dep.Version).0, CULTURE=NEUTRAL, PUBLICKEYTOKEN=NULL, PROCESSORARCHITECTURE=MSIL"
        }
        Write-Host "  Found: $($dep.Name) v$($dep.Version) -> $dllPath" -ForegroundColor Gray
    } else {
        Write-Host "  WARNING: $($dep.Name) not found, skipping" -ForegroundColor Yellow
    }
}

# ============================================================
# Step 3: Inject into .addin OPC package
# ============================================================

Write-Host "`n[3/4] Injecting dependencies into .addin package..." -ForegroundColor Yellow

Add-Type -AssemblyName WindowsBase

# Backup original
$backupPath = "$AddinPath.bak"
Copy-Item $AddinPath $backupPath -Force
Write-Host "  Backed up to: $backupPath" -ForegroundColor Gray

$package = [System.IO.Packaging.Package]::Open($AddinPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite)

# Find Content_Types.xml part
$ctPart = $null
foreach ($part in $package.GetParts()) {
    if ($part.Uri.ToString() -match "Content_Types") {
        $ctPart = $part
        break
    }
}

# Find _rels/.rels part
$relsPart = $null
foreach ($part in $package.GetParts()) {
    if ($part.Uri.ToString() -eq "/_rels/.rels") {
        $relsPart = $part
        break
    }
}

$injectCount = 0
foreach ($dep in $resolvedDeps) {
    # Check if already exists
    $existingPart = $null
    foreach ($part in $package.GetParts()) {
        if ($part.Uri.ToString() -match [regex]::Escape($dep.Name.ToUpper())) {
            $existingPart = $part
            break
        }
    }

    if ($existingPart) {
        Write-Host "  Already present: $($dep.Name)" -ForegroundColor Gray
        continue
    }

    # Create the assembly part in LocalAssemblyCache
    $partUri = "/LocalAssemblyCache/$($dep.OpcEntryName)"
    try {
        $uri = [System.IO.Packaging.PackUriHelper]::CreatePartUri(
            (New-Object System.Uri($partUri, [System.UriKind]::Relative))
        )
        $assemblyPart = $package.CreatePart($uri, "application/octet-stream")

        # Copy DLL content
        $sourceStream = [System.IO.File]::OpenRead($dep.DllPath)
        $targetStream = $assemblyPart.GetStream()
        $sourceStream.CopyTo($targetStream)
        $sourceStream.Close()
        $targetStream.Close()

        $injectCount++
        Write-Host "  Injected: $($dep.Name) v$($dep.Version)" -ForegroundColor Green
    } catch {
        Write-Host "  ERROR injecting $($dep.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Update Content_Types.xml to include dll type if not present
if ($ctPart) {
    $ctReader = New-Object System.IO.StreamReader($ctPart.GetStream())
    $ctXml = $ctReader.ReadToEnd()
    $ctReader.Close()

    if ($ctXml -notmatch 'Extension="dll"') {
        $ctXml = $ctXml.Replace(
            '</Types>',
            '<Default Extension="dll" ContentType="application/octet-stream" /></Types>'
        )
        $ctStream = $ctPart.GetStream([System.IO.FileMode]::Create)
        $ctWriter = New-Object System.IO.StreamWriter($ctStream)
        $ctWriter.Write($ctXml)
        $ctWriter.Close()
        Write-Host "  Updated Content_Types.xml" -ForegroundColor Gray
    }
}

$package.Close()

Write-Host "  Injected $injectCount assemblies" -ForegroundColor Green

# ============================================================
# Step 4: Deploy to TIA Portal UserAddIns
# ============================================================

if (-not $SkipDeploy) {
    Write-Host "`n[4/4] Deploying to TIA Portal UserAddIns..." -ForegroundColor Yellow

    $userAddIns = "$env:APPDATA\Siemens\Automation\Portal V21\UserAddIns"
    if (Test-Path $userAddIns) {
        Copy-Item $AddinPath "$userAddIns\" -Force
        Write-Host "  Deployed to: $userAddIns" -ForegroundColor Green
        Write-Host ""
        Write-Host "  IMPORTANT: Restart TIA Portal to load the updated Add-In" -ForegroundColor Yellow
    } else {
        Write-Host "  WARNING: UserAddIns directory not found" -ForegroundColor Yellow
        Write-Host "  Manually copy: $AddinPath" -ForegroundColor Yellow
        Write-Host "  To: $userAddIns" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n[4/4] Deploy skipped (-SkipDeploy)" -ForegroundColor Gray
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "Package: $AddinPath" -ForegroundColor Gray
Write-Host "Backup:  $backupPath" -ForegroundColor Gray
