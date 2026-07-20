# Running TIA Portal Code Agent End-to-End

Step-by-step guide for running the full system. Prerequisites are assumed installed.

## Quick Start (TL;DR)

```powershell
cd C:\github\tia-portal-code-agent

# 1. Build, test, package, install
.\build.ps1 all
.\build.ps1 install

# 2. Start TiaAgent.Bridge (port 43119)
dotnet run --project src/TiaAgent.Bridge --configuration Release

# 3. Start MiMoCode agent server (port 43120)
node C:\nvm4w\nodejs\node_modules\@mimo-ai\cli\bin\mimo serve --port 43120

# 4. In TIA Portal: Options > Settings > Add-Ins > activate "TIA Portal Code Agent"
# 5. Right-click a PLC block > AI Assistant > Explain selected object
```

## Prerequisites

| Component | Version | Check |
|---|---|---|
| Windows | 10/11 x64 | - |
| TIA Portal V21 | With Openness | `C:\Program Files\Siemens\Automation\Portal V21` |
| .NET SDK | 8.0+ | `dotnet --version` |
| .NET Framework | 4.8 | Registry: `HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` (Release >= 528040) |
| TiaMcpServer | 2.3.1+ | `dotnet tool list -g` shows `tiamcpserver` |
| MiMoCode / OpenCode | latest | `npm list -g @mimo-ai/cli` |
| Openness group | Member of `Siemens TIA Openness` | Check via `whoami /groups` |

## Step 1: Build and Test

```powershell
cd C:\github\tia-portal-code-agent

# Full build + test + package
.\build.ps1 all

# Or step by step
.\build.ps1 build      # Compile (Release)
.\build.ps1 test       # Run unit + architecture tests
.\build.ps1 pack       # Generate .addin OPC package
```

Expected output:
- Build: 0 errors
- Tests: 14 architecture tests passed
- Pack: `artifacts\TiaAgent-0-1-0.addin` created

## Step 2: Install the Add-In

```powershell
.\build.ps1 install
```

This copies the `.addin` file to:
```
%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\
```

If the folder doesn't exist, TIA Portal creates it on first launch.

## Step 3: Verify tia-mcp

```powershell
# Check it's installed
dotnet tool list -g

# Validate TIA environment
tia-mcp doctor
```

`tia-mcp` uses **stdio transport** -- it's launched automatically by the agent runtime when MCP tools are called. No separate process needed.

## Step 4: Start TiaAgent.Bridge

The Add-In communicates with the Bridge on port 43119. The Bridge forwards requests to OpenCode.

```powershell
dotnet run --project src/TiaAgent.Bridge --configuration Release
```

Verify it's running:
```powershell
netstat -ano | Select-String ":43119"
# Should show: TCP 127.0.0.1:43119 ... LISTENING
```

The Bridge stores its auth token at `%LOCALAPPDATA%\TiaAgent\bridge.token`. Check the log at `%LOCALAPPDATA%\TiaAgent\bridge.log`.

## Step 5: Start the Agent Runtime (MiMoCode)

The Bridge connects to the agent runtime via HTTP on port 43120. The agent runtime launches `tia-mcp` via stdio when it needs to call MCP tools.

### Option A: Start MiMoCode as a headless server (recommended for E2E)

```powershell
# Run from the project directory
node C:\nvm4w\nodejs\node_modules\@mimo-ai\cli\bin\mimo serve --port 43120
```

**Important:** Do NOT use `Start-Process -FilePath "mimo"` -- the `.ps1` wrapper opens in Notepad on Windows. Always run via `node` directly.

Verify it's running:
```powershell
netstat -ano | Select-String ":43120"
# Should show: TCP 127.0.0.1:43120 ... LISTENING
```

### Option B: Use MiMoCode TUI (interactive)

```powershell
mimo
```

This opens the TUI. The MCP config in `config/opencode.json` is used automatically.

### MCP Configuration

The agent runtime reads `config/opencode.json`:

```json
{
  "server": { "port": 43120 },
  "mcp": {
    "tia-portal": {
      "type": "local",
      "command": ["tia-mcp"],
      "enabled": true
    }
  },
  "agents": {
    "default": "tia-explain",
    "available": ["tia-explain", "tia-review", "tia-change"]
  },
  "model": {
    "provider": "openai",
    "model": "gpt-4o"
  }
}
```

The agent runtime spawns `tia-mcp` as a child process via stdio -- no separate MCP server process needed.

## Step 6: Activate the Add-In in TIA Portal

1. Open TIA Portal V21
2. Open a project with a PLC
3. Go to **Options > Settings > Add-Ins**
4. Enable **"TIA Portal Code Agent"**
5. Confirm any permission prompts

## Step 7: Use It

Right-click any object in the project tree:

- **AI Assistant > Explain selected object** -- read-only explanation
- **AI Assistant > Review selected object** -- reads + suggestions
- **AI Assistant > Propose change** -- reads + change proposal (MVP: read-only)

The Add-In sends the task to the Bridge (port 43119), which forwards it to MiMoCode (port 43120). MiMoCode launches `tia-mcp` to read from TIA Portal via Openness.

## Troubleshooting

### Bridge not running

The Add-In requires TiaAgent.Bridge to be running on port 43119. If you see "The local TIA Agent Bridge is not running", start it:

```powershell
dotnet run --project src/TiaAgent.Bridge --configuration Release
```

### Add-In not showing in TIA Portal

- Ensure TIA Portal was restarted after install
- Check `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\` contains the `.addin` file
- Check the log: `%LOCALAPPDATA%\TiaAgent\addin.log`

### Port 43119 already in use (Bridge)

```powershell
netstat -ano | Select-String ":43119"
# Kill the process using that port
```

### Port 43120 already in use (OpenCode)

```powershell
netstat -ano | Select-String ":43120"
# Kill the process using that port, or use a different port:
node ... mimo serve --port 43121
# Then update config/opencode.json server.port to 43121
```

### tia-mcp fails to connect to TIA Portal

```powershell
tia-mcp doctor
```

Common issues:
- TIA Portal not open
- No project loaded
- User not in `Siemens TIA Openness` group
- TIA Portal version mismatch

### Agent not responding

Check if Bridge is running:
```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:43119/health" -Headers @{Authorization="Bearer $(Get-Content $env:LOCALAPPDATA\TiaAgent\bridge.token)"}
```

Check if MiMoCode server is running:
```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:43120/" -Method Get
```

If the server is down, restart it (see Step 4).

## Logs

| Log | Path | Contents |
|---|---|---|
| Add-In log | `%LOCALAPPDATA%\TiaAgent\addin.log` | Action triggers, Bridge client calls, results |
| Bridge log | `%LOCALAPPDATA%\TiaAgent\bridge.log` | Task lifecycle, OpenCode calls, errors |

## Architecture Flow

```
User right-clicks object in TIA Portal
    |
    v
Add-In (TiaAgent.AddIn) -- captures selection, creates BridgeTaskRequest
    |
    v (HTTP on port 43119)
TiaAgent.Bridge -- task management, OpenCode session
    |
    v (HTTP on port 43120)
OpenCode/MiMoCode -- AI agent runtime, model integration
    |
    v (stdio, spawned as child process)
tia-mcp (Czarnak/tia-portal-mcp) -- MCP server
    |
    v (.NET Openness SDK)
TIA Portal Openness -- reads project data
    |
    v (results flow back up the chain)
Add-In displays result in AssistantPanel or popup
```

## Key Files

| File | Purpose |
|---|---|
| `build.ps1` | Build, test, pack, install commands |
| `TiaAgent.sln` | Solution with 5 source + 3 test projects |
| `config/opencode.json` | Agent runtime + MCP configuration |
| `config/bridge.example.json` | Bridge configuration example |
| `agents/tia-explain.md` | Read-only agent profile |
| `agents/tia-review.md` | Review agent profile |
| `agents/tia-change.md` | Change agent profile |
| `docs/spec/ARCHITECTURE.md` | Authoritative architecture contract |
