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

The MCP server is spawned automatically by the selected agent runtime when configured:

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

## Runtime Supervisor (Recommended)

The Runtime Supervisor provides a single command to start, monitor, and stop all services:

```powershell
# Start all services (Bridge + Agent Runtime)
.\src\runtime\Scripts\run.ps1

# Check status
.\src\runtime\Scripts\status.ps1

# Stop all services
.\src\runtime\Scripts\stop.ps1
```

### Available Commands

| Command | Description |
|---------|-------------|
| `run.ps1` | Start and monitor all services |
| `stop.ps1` | Gracefully stop all services |
| `status.ps1` | Show runtime status and health |

### Options

```powershell
# Start with verbose logging
.\src\runtime\Scripts\run.ps1 -Verbose

# Start and exit (no monitoring)
.\src\runtime\Scripts\run.ps1 -NoMonitor

# JSON status output
.\src\runtime\Scripts\status.ps1 -Json

# Force stop (skip graceful shutdown)
.\src\runtime\Scripts\stop.ps1 -Force
```

### Runtime Directory

All runtime data is stored in `%LOCALAPPDATA%\TiaAgent\`:

```
%LOCALAPPDATA%\TiaAgent\
├── config\settings.json    # Supervisor configuration
├── runtime\runtime.json    # Service discovery manifest
├── runtime\secrets\        # Transient credentials
├── logs\                   # Service logs
└── scripts\                # Runtime scripts
```

## Manual Bridge Startup (Legacy)

If you need to start the Bridge manually without the Runtime Supervisor:

```powershell
# Start the Bridge (required for AI Assistant actions)
dotnet run --project src/TiaAgent.Bridge --configuration Release
```

## How Packaging Works (CRITICAL)

The packaging uses a **2-step approach** — this is the ONLY method that produces a .addin TIA Portal loads:

1. **Siemens Publisher.exe** creates the base OPC package from `Config.xml`
2. **Sign with OpcSigner** (self-signed certificate)

The Add-In now only requires `TiaAgent.AddIn.dll` and `TiaAgent.Contracts.dll` — no transitive NuGet dependencies are injected.

## Verification

```powershell
.\build.ps1 verify   # Check package structure
```

## Add-In Features

- **"AI Assistant"** context menu: Explain, Review, Propose change (requires agent runtime + MCP server)
- **"TIA Agent Diagnostics"** context menu: Test Integration (self-contained, no dependencies)

## How to Test

1. Install MCP server: `dotnet tool install -g TiaMcpServer`
2. Validate: `tia-mcp doctor`
3. Start Runtime Supervisor: `.\src\runtime\Scripts\run.ps1`
4. Open TIA Portal V21 with a project
5. Right-click in Project Tree → **TIA Agent Diagnostics** → **Test Integration**
6. MessageBox confirms the Add-In is functional
7. Right-click a PLC block → **AI Assistant** → **Explain selected object**
8. Check `%LOCALAPPDATA%\TiaAgent\addin.log` for diagnostic entries

## End-to-End Flow

```
User right-clicks block in TIA Portal
  → ProjectTreeProvider captures selection snapshot
  → AgentBridgeClient.StartTaskAsync (background thread)
    → HTTP POST to Bridge (port 43119)
      → Bridge selects runtime adapter (Mimo, OpenCode, or Claude)
      → Runtime spawns tia-mcp (stdio child process)
        → Agent calls execute_read_batch
        → Agent generates explanation
      → Bridge returns response
    → Add-In polls for completion
  → MessageBox shows result
```

## Service Discovery

The Add-In discovers services via the runtime manifest:

1. Reads `%LOCALAPPDATA%\TiaAgent\runtime\runtime.json`
2. Validates the schema version and status
3. Calls the service's health endpoint before use

**Important:** `runtime.json` is discovery metadata, not proof of service health. Always validate the health endpoint.

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
- **OpenCode unavailable**: Ensure Runtime Supervisor is running: `.\src\runtime\Scripts\status.ps1`
- **Bridge not running**: Start Runtime Supervisor: `.\src\runtime\Scripts\run.ps1`
- **Port conflict**: Runtime Supervisor automatically allocates alternative ports from range 43100-43200
