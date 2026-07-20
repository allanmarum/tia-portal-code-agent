# TIA Portal V21 Add-In — Agent Instructions

## Environment

- **TIA Portal V21** installed at: `C:\Program Files\Siemens\Automation\Portal V21`
- **All Siemens assemblies** in: `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\`
- **Publisher.exe**: `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\Siemens.Engineering.AddIn.Publisher.exe`
- **MCP Server**: [Czarnak/tia-portal-mcp](https://github.com/Czarnak/tia-portal-mcp) — install with `dotnet tool install -g TiaMcpServer`

## MCP Server Setup

The project uses Czarnak's external MCP server instead of a custom one. It communicates via stdio transport (no HTTP server needed).

```powershell
# Install
dotnet tool install -g TiaMcpServer

# Validate environment
tia-mcp doctor

# Test with MCP Inspector
npx -y @modelcontextprotocol/inspector tia-mcp
```

The MCP server is spawned automatically by OpenCode when configured in `config/opencode.json`:

```json
{
  "mcp": {
    "tia-portal": {
      "type": "local",
      "command": ["tia-mcp"],
      "enabled": true
    }
  }
}
```

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

- **"AI Assistant"** context menu: Explain, Review, Propose change (requires OpenCode + MCP server)
- **"TIA Agent Diagnostics"** context menu: Test Integration (self-contained, no dependencies)

## How to Test

1. Install MCP server: `dotnet tool install -g TiaMcpServer`
2. Validate: `tia-mcp doctor`
3. Open TIA Portal V21 with a project
4. Right-click in Project Tree → **TIA Agent Diagnostics** → **Test Integration**
5. MessageBox confirms the Add-In is functional
6. Right-click a PLC block → **AI Assistant** → **Explain selected object**
7. Check `%LOCALAPPDATA%\TiaAgent\addin.log` for diagnostic entries

## End-to-End Flow

```
User right-clicks block in TIA Portal
  → ProjectTreeProvider captures object name/type
  → OpenCodeOrchestrator.ExecuteTaskAsync (background thread)
    → HTTP POST to OpenCode (port 43120)
      → OpenCode spawns tia-mcp (stdio child process)
        → tia-mcp attaches to running TIA Portal
      → Agent calls execute_read_batch (browse_project_tree, get_block_content)
      → Agent generates explanation
    → Response returned via SSE
  → MessageBox shows result
```

## Key Technical Details

- **Provider constructor MUST take `TiaPortal`**: TIA Portal passes it automatically
- **Config.xml**: Uses `DisplayInMultiuser`, `UnrestrictedPermissions`, `TIA.ReadWrite`
- **SIEMENS flag**: Auto-detected in `Directory.Build.props` when Siemens assemblies exist
- **All Siemens refs**: `Private=false`, resolved from TIA at runtime
- **MCP server**: External process (Czarnak's tia-mcp), stdio transport, no HTTP port

## Troubleshooting

- **Red X on Add-In**: Rebuild with `.\build.ps1 all` — old packages may be stale
- **Context menus missing**: Check `%LOCALAPPDATA%\TiaAgent\addin.log`
- **Build fails**: Verify TIA Portal V21 is installed
- **MCP server not found**: Run `tia-mcp doctor` to validate installation
- **OpenCode unavailable**: Ensure OpenCode is running on port 43120
