# TIA Portal V21 Add-In — Agent Instructions

## Environment

- **TIA Portal V21** installed at: `C:\Program Files\Siemens\Automation\Portal V21`
- **All Siemens assemblies** in: `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\`
- **Publisher.exe**: `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\Siemens.Engineering.AddIn.Publisher.exe`

## Build + Package + Install (one command)

```powershell
.\build.ps1 all
```

Or step by step:

```powershell
.\build.ps1 build    # Compile
.\build.ps1 pack     # Package with Publisher + deps + sign
.\build.ps1 install  # Copy to UserAddIns
```

## How Packaging Works (CRITICAL)

The packaging uses a **3-step hybrid approach** — this is the ONLY method that produces a .addin TIA Portal loads:

1. **Siemens Publisher.exe** creates the base OPC package from `Config.xml`
2. **Inject transitive NuGet deps** into `LocalAssemblyCache/` using `System.IO.Packaging`
3. **Sign with OpcSigner** (self-signed certificate)

Do NOT use manual `System.IO.Packaging`-only — TIA Portal rejects those packages.
Do NOT use Publisher-only — it misses transitive deps (System.Text.Json, etc.).

## Verification

```powershell
.\build.ps1 verify   # Check package structure
```

## Add-In Features

- **"AI Assistant"** context menu: Explain, Review, Propose change (requires MCP server)
- **"TIA Agent Diagnostics"** context menu: Test Integration (self-contained, no dependencies)

## How to Test

1. Open TIA Portal V21
2. Open or create a project
3. Right-click in Project Tree → **TIA Agent Diagnostics** → **Test Integration**
4. MessageBox confirms the Add-In is functional
5. Check `%LOCALAPPDATA%\TiaAgent\addin.log` for diagnostic entries

## Key Technical Details

- **Provider constructor MUST take `TiaPortal`**: TIA Portal passes it automatically
- **Config.xml**: Uses `DisplayInMultiuser`, `UnrestrictedPermissions`, `TIA.ReadWrite`
- **SIEMENS flag**: Auto-detected in `Directory.Build.props` when Siemens assemblies exist
- **All Siemens refs**: `Private=false`, resolved from TIA at runtime

## Troubleshooting

- **Red X on Add-In**: Rebuild with `.\build.ps1 all` — old packages may be stale
- **Context menus missing**: Check `%LOCALAPPDATA%\TiaAgent\addin.log`
- **Build fails**: Verify TIA Portal V21 is installed
