# Running TIA Portal Code Agent End-to-End

Step-by-step guide for running the full system. Prerequisites are assumed installed.

## Quick Start (TL;DR)

```powershell
cd C:\github\tia-portal-code-agent

# 1. Build, test, package, install
.\build.ps1 all
.\build.ps1 install

# 2. Start all services with Runtime Supervisor
.\src\runtime\Scripts\run.ps1

# 3. In TIA Portal: Options > Settings > Add-Ins > activate "TIA Portal Code Agent"
# 4. Right-click a PLC block > AI Assistant > Explain selected object
```

## Prerequisites

| Component | Version | Check |
|---|---|---|
| Windows | 10/11 x64 | - |
| TIA Portal V21 | With Openness | `C:\Program Files\Siemens\Automation\Portal V21` |
| .NET SDK | 8.0+ | `dotnet --version` |
| .NET Framework | 4.8 | Registry: `HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` (Release >= 528040) |
| TiaMcpServer | 2.3.1+ | `dotnet tool list -g` shows `tiamcpserver` |
| **One agent runtime** | latest | See [Supported Runtimes](#supported-runtimes) below |
| Openness group | Member of `Siemens TIA Openness` | Check via `whoami /groups` |

### Supported Runtimes

At least one agent runtime must be installed. The Bridge supports three interchangeable runtimes:

| Runtime | ID | Install | Check |
|---|---|---|---|
| Mimo CLI | `mimo` | `npm install -g @mimo-ai/cli` | `mimo --version` |
| OpenCode | `opencode` | `npm install -g opencode` | `opencode --version` |
| Claude Code CLI | `claude` | `npm install -g @anthropic-ai/claude-code` | `claude --version` |

The default runtime is `opencode`. To change it, edit `%LOCALAPPDATA%\TiaAgent\config.json` or set the `TIA_AGENT_RUNTIME` environment variable. See [docs/RUNTIME.md](RUNTIME.md) for full configuration details.

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
- Tests: All tests passed
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

## Step 4: Configure the Runtime (Optional)

The runtime configuration file is at `%LOCALAPPDATA%\TiaAgent\config.json`. If it doesn't exist, the Bridge uses `opencode` as the default runtime.

```json
{
  "defaultRuntime": "opencode",
  "runtimes": {
    "mimo": { "enabled": true, "executable": "mimo" },
    "opencode": { "enabled": true, "mode": "server", "executable": "opencode" },
    "claude": { "enabled": true, "executable": "claude" }
  }
}
```

You can also override the runtime per-task or via environment variable:
```powershell
$env:TIA_AGENT_RUNTIME = "claude"
```

See [docs/RUNTIME.md](RUNTIME.md) for all configuration options.

## Step 5: Start All Services

### Option A: Runtime Supervisor (Recommended)

The Runtime Supervisor starts and manages all services with a single command. It is runtime-aware: it reads `config.json` to determine which runtime to start, and only launches a server process when the selected runtime is in server mode.

```powershell
.\src\runtime\Scripts\run.ps1
```

This will:
1. Validate prerequisites (including the selected runtime)
2. Allocate ports (Bridge: 43119)
3. Start the Bridge process
4. If the runtime is in **server mode**: start the runtime server and wait for health
5. If the runtime is in **CLI mode**: verify the executable is available (no server needed)
6. Monitor service health
7. Publish a runtime manifest for service discovery

**Available commands:**

```powershell
# Start and monitor all services
.\src\runtime\Scripts\run.ps1

# Check runtime status
.\src\runtime\Scripts\status.ps1

# JSON status for automation
.\src\runtime\Scripts\status.ps1 -Json

# Stop all services gracefully
.\src\runtime\Scripts\stop.ps1

# Force stop (skip graceful shutdown)
.\src\runtime\Scripts\stop.ps1 -Force
```

**Runtime Directory:**

All runtime data is stored in `%LOCALAPPDATA%\TiaAgent\`:

```
%LOCALAPPDATA%\TiaAgent\
├── config.json              # Runtime selection configuration
├── config\settings.json     # Supervisor configuration
├── runtime\runtime.json     # Service discovery manifest
├── runtime\secrets\         # Transient credentials
├── logs\                    # Service logs
└── scripts\                 # Runtime scripts
```

### Option B: Manual Startup (Legacy)

If you need to start services individually:

```powershell
# Start TiaAgent.Bridge (port 43119)
dotnet run --project src/TiaAgent.Bridge --configuration Release

# If using OpenCode in server mode (port 43120):
opencode serve --port 43120
```

For CLI-mode runtimes (mimo, claude), no server process is needed. The Bridge spawns the runtime process for each task.

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

The Add-In sends the task to the Bridge (port 43119), which resolves the configured runtime and forwards the task. The runtime launches `tia-mcp` to read from TIA Portal via Openness.

The task response includes which runtime executed it (e.g. `[Runtime: claude]`).

## Service Discovery

The Add-In discovers services via the runtime manifest:

1. Reads `%LOCALAPPDATA%\TiaAgent\runtime\runtime.json`
2. Validates the schema version and status
3. Calls the service's health endpoint before use

The manifest includes runtime information:
```json
{
  "status": "ready",
  "runtime": {
    "id": "claude",
    "displayName": "Claude Code CLI",
    "mode": "cli",
    "healthy": true
  },
  "services": {
    "bridge": { "status": "healthy", "port": 43119 }
  }
}
```

**Important:** `runtime.json` is discovery metadata, not proof of service health. Always validate the health endpoint.

## Bridge API

The Bridge exposes these endpoints:

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | No | Bridge + runtime health |
| `POST` | `/v1/tasks` | Bearer | Create a task (may include `"runtime": "claude"`) |
| `GET` | `/v1/tasks/{id}` | Bearer | Poll task status (includes `runtimeId`) |
| `POST` | `/v1/tasks/{id}/cancel` | Bearer | Cancel a task |
| `GET` | `/api/runtimes` | Bearer | List all runtimes with availability |
| `GET` | `/api/runtimes/{id}/health` | Bearer | Check specific runtime health |
| `GET` | `/api/settings/runtime` | Bearer | Get default runtime |
| `PUT` | `/api/settings/runtime` | Bearer | Change default runtime |
| `GET` | `/diagnostics` | Bearer | Bridge diagnostics |

## Troubleshooting

### Runtime Supervisor

Check if the runtime is running:
```powershell
.\src\runtime\Scripts\status.ps1
```

View supervisor logs:
```powershell
Get-Content "$env:LOCALAPPDATA\TiaAgent\logs\supervisor.log" -Tail 50
```

### Bridge not running

The Add-In requires TiaAgent.Bridge to be running on port 43119. If you see "The local TIA Agent Bridge is not running", start the supervisor:

```powershell
.\src\runtime\Scripts\run.ps1
```

### Runtime not found

```
Unknown runtime 'xyz'. Available runtimes: mimo, opencode, claude
```

Check the runtime ID in your config or task request. Run `.\src\runtime\Scripts\status.ps1` to see available runtimes.

### Runtime unavailable

```
Selected runtime `mimo` is unavailable.
Executable: mimo
Reason: executable not found
```

Install the runtime CLI or update the `executable` path in `%LOCALAPPDATA%\TiaAgent\config.json`.

### Add-In not showing in TIA Portal

- Ensure TIA Portal was restarted after install
- Check `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\` contains the `.addin` file
- Check the log: `%LOCALAPPDATA%\TiaAgent\addin.log`

### Port 43119 already in use (Bridge)

```powershell
netstat -ano | Select-String ":43119"
# Kill the process using that port
```

The Runtime Supervisor will automatically allocate a different port from the configured range (43100-43200).

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

Check if services are healthy:
```powershell
# Check Bridge health (includes runtime info)
Invoke-RestMethod -Uri "http://127.0.0.1:43119/health"

# List runtimes and their availability
$token = Get-Content "$env:LOCALAPPDATA\TiaAgent\runtime\secrets\bridge.token"
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "http://127.0.0.1:43119/api/runtimes" -Headers $headers
```

If services are down, restart the supervisor:
```powershell
.\src\runtime\Scripts\stop.ps1
.\src\runtime\Scripts\run.ps1
```

### Silent fallback disabled

If the selected runtime fails, the Bridge does **not** automatically switch to another runtime. You will see an actionable error listing available alternatives. To use a different runtime, either:

- Set `TIA_AGENT_RUNTIME` environment variable, or
- Edit `%LOCALAPPDATA%\TiaAgent\config.json`, or
- Include `"runtime": "claude"` in the task request

## Logs

| Log | Path | Contents |
|---|---|---|
| Supervisor log | `%LOCALAPPDATA%\TiaAgent\logs\supervisor.log` | Startup, shutdown, health checks, errors |
| Add-In log | `%LOCALAPPDATA%\TiaAgent\addin.log` | Action triggers, Bridge client calls, results |
| Bridge log | `%LOCALAPPDATA%\TiaAgent\logs\bridge.log` | Task lifecycle, runtime calls, errors |

## Architecture Flow

```
User right-clicks object in TIA Portal
    |
    v
Add-In (TiaAgent.AddIn) -- captures selection, creates BridgeTaskRequest
    |
    v (HTTP on port 43119)
TiaAgent.Bridge -- task management, runtime resolution
    |
    v (IAgentRuntime abstraction)
    +--- MimoCliRuntime (mimo run --format json)
    +--- OpenCodeRuntime (HTTP server or opencode run)
    +--- ClaudeCodeRuntime (claude -p --output-format json)
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
| `TiaAgent.sln` | Solution with 5 source + 5 test projects |
| `src\runtime\Scripts\run.ps1` | Runtime Supervisor - start all services |
| `src\runtime\Scripts\stop.ps1` | Runtime Supervisor - stop all services |
| `src\runtime\Scripts\status.ps1` | Runtime Supervisor - check status |
| `%LOCALAPPDATA%\TiaAgent\config.json` | Runtime selection configuration |
| `config\opencode.json` | Agent runtime + MCP configuration |
| `config\settings.example.json` | Supervisor settings template |
| `config\bridge.example.json` | Bridge configuration example |
| `agents\tia-explain.md` | Read-only agent profile |
| `agents\tia-review.md` | Review agent profile |
| `agents\tia-change.md` | Change agent profile |
| `docs\RUNTIME.md` | Runtime configuration guide |
| `docs\spec\ARCHITECTURE.md` | Authoritative architecture contract |
