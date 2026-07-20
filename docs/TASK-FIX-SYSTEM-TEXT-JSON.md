# Task: Fix System.Text.Json Assembly Loading in Add-In

## Problem

The Add-In fails at runtime with:

```
System.IO.FileNotFoundException: Could not load file or assembly
'System.Text.Json, Version=8.0.0.5, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
or one of its dependencies. The system cannot find the file specified.

at TiaAgent.AddIn.Providers.TiaAgentContextMenu.RunViaOpenCode(...)
at TiaAgent.AddIn.Providers.TiaAgentContextMenu.HandleAction(...)
```

This happens when the user triggers "Explain", "Review", or "Propose" from the AI Assistant context menu in TIA Portal.

## Root Cause Analysis

The Add-In uses `System.Text.Json` (via `TiaAgent.OpenCode` -> `OpenCodeHttpClient`) to serialize/deserialize JSON when communicating with the agent runtime on port 43120.

**Build output has the DLL:**
`src\TiaAgent.AddIn\bin\Release\net48\System.Text.Json.dll` exists (644,888 bytes).

**But TIA Portal can't find it at runtime.** The Siemens Publisher only packages the 4 project assemblies:
- TiaAgent.AddIn.dll
- TiaAgent.Contracts.dll
- TiaAgent.Application.dll
- TiaAgent.OpenCode.dll

It does NOT include transitive NuGet dependencies like `System.Text.Json.dll`.

## Current Packaging Flow

1. `build.ps1 pack` runs `Siemens.Engineering.AddIn.Publisher.exe` -> creates `.addin` with 4 assemblies
2. `build.ps1 pack` then runs OPC injection step to add transitive deps into `/LocalAssemblyCache/`
3. `build.ps1 install` copies `.addin` to `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\`

The injection step in `build.ps1` (lines 177-230) already attempts to inject these DLLs:
- System.Text.Json.dll
- System.Text.Encodings.Web.dll
- System.Buffers.dll
- System.Memory.dll
- System.Runtime.CompilerServices.Unsafe.dll
- Microsoft.Bcl.AsyncInterfaces.dll
- System.Threading.Tasks.Extensions.dll
- System.Numerics.Vectors.dll
- Microsoft.Extensions.DependencyInjection.Abstractions.dll
- Microsoft.Extensions.Logging.Abstractions.dll

**The injection may be failing silently** (the `catch` block shows "Skipped (exists)" which masks actual errors), or TIA Portal's OPC loader doesn't resolve assemblies from `/LocalAssemblyCache/`.

## Possible Fixes (investigate and pick the right one)

### Option A: Fix the injection in build.ps1

The injection step writes to `/LocalAssemblyCache/NAME,%20VERSION=...`. Verify:
1. Is the URI format correct for TIA Portal V21's OPC loader?
2. Does TIA Portal actually look in `/LocalAssemblyCache/` for assemblies?
3. Is the injection actually succeeding? Add verbose logging to the catch block.

### Option B: Include DLLs in the Publisher's assembly list

Check if `Config.xml` can declare additional assemblies beyond the project references. The Publisher documentation may support declaring transitive deps.

### Option C: Use binding redirect or codebase hint

Add an `.addin` manifest or binding redirect that tells TIA Portal where to find `System.Text.Json.dll`.

### Option D: Downgrade System.Text.Json to a version already loaded by TIA Portal

If TIA Portal or .NET Framework 4.8 already loads a compatible version, use that instead.

### Option E: Avoid System.Text.Json in the net48 project

Use `Newtonsoft.Json` (already in .NET Framework) or manual string concatenation for the simple HTTP calls in `OpenCodeHttpClient.cs`. This avoids the dependency entirely.

## Verification

After the fix:
1. Rebuild: `.\build.ps1 all`
2. Reinstall: `.\build.ps1 install`
3. Restart TIA Portal
4. Activate the Add-In
5. Right-click a PLC block > AI Assistant > Explain selected object
6. Check `%LOCALAPPDATA%\TiaAgent\debug.log` -- should NOT contain `FileNotFoundException`
7. The action should complete and show a result (or a connection error if MiMoCode isn't running, which is fine)

## Relevant Files

| File | Purpose |
|---|---|
| `build.ps1` (lines 177-230) | Transitive dependency injection into `.addin` package |
| `src\TiaAgent.AddIn\TiaAgent.AddIn.csproj` | Project references and conditional Siemens assemblies |
| `src\TiaAgent.OpenCode\TiaAgent.OpenCode.csproj` | References `System.Text.Json` v8.0.5 |
| `src\TiaAgent.OpenCode\Client\OpenCodeHttpClient.cs` | Uses `JsonSerializer.Serialize/Deserialize` |
| `src\TiaAgent.AddIn\bin\Release\net48\publisher_log.txt` | Publisher output (shows only 4 assemblies packaged) |
| `%LOCALAPPDATA%\TiaAgent\debug.log` | Runtime error log |
| `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\` | Installed `.addin` location |
