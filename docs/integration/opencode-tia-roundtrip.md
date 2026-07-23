# TIA Portal ↔ Agent Runtime Roundtrip Integration

## Architecture

```text
TIA Portal Add-In (UI, commands, selection capture)
  ↓ Runtime Discovery (reads runtime.json)
TiaAgent.Bridge (.NET 8, task management, runtime adapters)
  ↓ HTTP (127.0.0.1:43119)
Agent Runtime (Mimo CLI, OpenCode, or Claude Code)
  ↓ CLI or HTTP
Czarnak's tia-mcp (.NET 8 MCP server)
  ↓ stdio (MCP protocol)
OpennessWorker (.NET 4.8)
  ↓ TIA Portal Openness API
TIA Portal V21
```

### Runtime Supervisor

The Runtime Supervisor orchestrates the startup of Bridge and OpenCode:

```powershell
# Start all services
.\src\runtime\Scripts\run.ps1

# Check status
.\src\runtime\Scripts\status.ps1

# Stop all services
.\src\runtime\Scripts\stop.ps1
```

See `docs/RUN.md` for detailed usage.

### Data Flow (Explain Selected Object)

```text
1.  User selects FB_Conveyor in TIA Portal project tree
2.  User clicks: AI Assistant → Explain selected object
3.  Add-In captures object name ("FB_Conveyor") and type ("PlcBlock")
4.  Add-In creates BridgeTaskRequest and sends to Bridge (port 43119)
5.  Bridge creates/reuses OpenCode session and sends message
6.  OpenCode spawns tia-mcp via stdio (MCP transport)
7.  Agent calls execute_read_batch with browse_project_tree
8.  tia-mcp returns project tree (agent discovers block path)
9.  Agent calls execute_read_batch with get_block_content
10. tia-mcp exports block content via OpennessWorker
11. Agent generates explanation using the model
12. OpenCode returns final response via SSE
13. Orchestrator collects response
14. Add-In displays explanation in TIA Portal UI (MessageBox)
```

## Configuration

### OpenCode Configuration (`config/opencode.json`)

```json
{
  "$schema": "https://opencode.ai/config.json",
  "server": {
    "port": 43120
  },
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

### MCP Server (Czarnak/tia-portal-mcp)

Install: `dotnet tool install -g TiaMcpServer`
Validate: `tia-mcp doctor`

The MCP server is spawned automatically by OpenCode via stdio transport. No separate process management needed.

## MCP Tools (Czarnak/tia-portal-mcp)

### Batch Read Tools

| Tool | Description |
|---|---|
| `execute_read_batch` | Run up to 50 read operations in one call |

Read operations: `browse_project_tree`, `get_block_content`, `list_tag_tables`, `read_hardware_config`, `read_cross_references`, `search_equipment_catalog`, `compile_check`, `get_project_status`

### Batch Write Tools

| Tool | Description |
|---|---|
| `preview_write_batch` | Preview writes, returns `safetyToken` |
| `apply_write_batch` | Apply previewed writes with `safetyToken` |

### Project Lifecycle Tools

| Tool | Description |
|---|---|
| `get_project_status` | Project metadata |
| `open_project` | Open and bind to a project |
| `save_project` | Save the active project |
| `save_project_as` | Save to a copy directory |
| `close_project` | Close the project |
| `archive_project` | Archive the project |
| `create_project` | Create a new project |

## Agent Profiles

| Profile | File | Allowed Tools |
|---|---|---|
| tia-explain | `agents/tia-explain.md` | `execute_read_batch`, `get_project_status` |
| tia-review | `agents/tia-review.md` | `execute_read_batch`, `get_project_status` |
| tia-change | `agents/tia-change.md` | `execute_read_batch`, `get_project_status`, `preview_write_batch`, `apply_write_batch` |

## Manual Test Procedure

### Prerequisites

1. Install Czarnak's MCP server: `dotnet tool install -g TiaMcpServer`
2. Validate environment: `tia-mcp doctor`
3. Build the solution: `dotnet build TiaAgent.sln`
4. Start OpenCode with the config from `config/opencode.json`

### Test A: MCP Server Health

```powershell
tia-mcp doctor --project "C:\Projects\Line.ap21"
```

**Expected:** All checks pass (OS, .NET runtimes, TIA installation, Openness assemblies, user group).

### Test B: MCP Inspector (Standalone)

```powershell
npx -y @modelcontextprotocol/inspector tia-mcp
```

**Expected:** 10 tools listed. `execute_read_batch` with `browse_project_tree` returns project tree.

### Test C: Add-In → OpenCode Roundtrip

1. Start TIA Portal V21, open a test project
2. Select a PLC block in the project tree
3. Right-click → AI Assistant → Explain selected object

**Expected:** MessageBox shows the AI-generated explanation of the block.

### Test D: Failure Scenarios

| Scenario | Expected Behavior |
|---|---|
| OpenCode not running | `OPENCODE_UNAVAILABLE` error with user-friendly message |
| tia-mcp not installed | OpenCode fails to spawn MCP server |
| No TIA project open | `get_project_status` returns error |
| Unsupported selection | Agent reports unsupported object type |

## Error Codes

| Code | Description | User Message |
|---|---|---|
| `OPENCODE_UNAVAILABLE` | OpenCode server not reachable | "AI assistant not available. Ensure OpenCode is running." |
| `OPENCODE_TASK_FAILED` | Task execution failed | "AI assistant encountered an error. Please try again." |
| `TIA_NOT_CONNECTED` | TIA Portal not connected | "Not connected to TIA Portal." |
| `TIA_PROJECT_NOT_OPEN` | No project open | "No TIA Portal project is open." |
| `TIA_TIMEOUT` | Operation timed out | "Operation timed out. Please try again." |
| `TIA_CANCELLED` | Operation cancelled | "The operation was cancelled." |

## Correlation ID Propagation

Every command generates a correlation ID that flows through:

```text
Add-In command → OpenCode session/task → MCP tool calls → OpennessWorker → response
```

Format: `tia-<guid>`

## Architecture Decisions

1. **Czarnak's tia-mcp as MCP server** — Replaces our custom MCP host. Uses stdio transport (no HTTP server on port 43121).

2. **OpenCode spawns MCP server** — tia-mcp is launched as a child process by OpenCode. No separate process management.

3. **Selection context in prompt** — Object name/type are embedded in the agent message instead of using selection tokens (Czarnak's server has no token concept).

4. **Batch operations** — Agent combines related reads into single `execute_read_batch` calls for efficiency.

5. **Add-In calls Bridge via HTTP** — `ProjectTreeProvider.HandleAction` creates a `BridgeTaskRequest` and sends it to `TiaAgent.Bridge` on port 43119. The Bridge manages OpenCode sessions and task lifecycle.

## Known Limitations

1. **OpenCode API endpoints assumed** — The `OpenCodeHttpClient` uses a conventional REST API pattern. Verify against the actual OpenCode/MiMoCode API.

2. **No selection tokens** — Object context is passed as text in the prompt. The agent must use `browse_project_tree` to discover block paths.

3. **Czarnak's server lacks `get_call_hierarchy`** — Use `read_cross_references` as a partial substitute.

4. **Bridge must be running** — The Add-In requires TiaAgent.Bridge to be running on port 43119 before AI Assistant actions work.
