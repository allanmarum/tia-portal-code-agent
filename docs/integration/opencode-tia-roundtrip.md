# TIA Portal ↔ OpenCode Roundtrip Integration

## Architecture

```text
TIA Portal Add-In (UI, commands, selection capture)
  ↓ HTTP (127.0.0.1:43120)
OpenCode Agent Runtime (model interaction, tool-calling loop)
  ↓ MCP over HTTP (127.0.0.1:43121/mcp)
TIA MCP Server (tia_* tool handlers)
  ↓ delegates to
ITiaProjectService (single Openness adapter)
  ↓
TIA Portal Openness SDK
```

### Data Flow (Explain Selected Object)

```text
1. User selects FB_Conveyor in TIA Portal project tree
2. User clicks: AI Assistant → Explain selected object
3. Add-In captures selection snapshot (immutable)
4. Add-In generates selection token (sel-xxx)
5. Add-In sends task to OpenCode with selection metadata
6. OpenCode calls tia_get_current_selection(sel-xxx) via MCP
7. MCP handler resolves selection token → returns snapshot
8. OpenCode calls tia_read_block(objectId) via MCP
9. MCP handler delegates to ITiaProjectService → returns block source
10. OpenCode generates explanation using the model
11. OpenCode returns final response to Add-In
12. Add-In displays explanation in TIA Portal UI
```

## Configuration

### OpenCode Configuration (`config/opencode.json`)

```json
{
  "server": {
    "url": "http://127.0.0.1:43120",
    "port": 43120
  },
  "mcp": {
    "servers": {
      "tia-agent": {
        "url": "http://127.0.0.1:43121/mcp",
        "transport": "streamable-http"
      }
    }
  },
  "agents": {
    "default": "tia-explain",
    "available": ["tia-explain", "tia-review", "tia-change"]
  }
}
```

### MCP Server Configuration (`config/appsettings.json`)

```json
{
  "TiaAgent": {
    "Mode": "Simulator",
    "Mcp": {
      "Enabled": true,
      "Host": "127.0.0.1",
      "Port": 43121,
      "RequireAuthentication": true,
      "RequestTimeoutSeconds": 30
    },
    "OpenCode": {
      "Mode": "External",
      "BaseUrl": "http://127.0.0.1:43120",
      "DefaultAgent": "tia-explain",
      "StartupTimeoutSeconds": 20
    }
  }
}
```

## MCP Tools

### Diagnostic Tools

| Tool | Description | Risk |
|---|---|---|
| `tia_ping` | Validates MCP server connectivity and TIA project status | R0 |

### Context Tools

| Tool | Description | Risk |
|---|---|---|
| `tia_get_current_context` | Returns TIA Portal session context | R0 |
| `tia_get_current_selection` | Returns selection snapshot by token | R0 |

### Read Tools

| Tool | Description | Risk |
|---|---|---|
| `tia_list_blocks` | Lists PLC blocks with optional filters | R0 |
| `tia_read_block` | Reads block source code and interface | R0 |
| `tia_get_block_interface` | Returns block interface definition | R0 |

### Reference Tools

| Tool | Description | Risk |
|---|---|---|
| `tia_get_call_hierarchy` | Returns call hierarchy tree | R0 |
| `tia_find_references` | Finds block references | R0 |

### Compile Tools

| Tool | Description | Risk |
|---|---|---|
| `tia_compile_software` | Compiles software container | R1 |

### Change Tools (not used in MVP read-only flow)

| Tool | Description | Risk |
|---|---|---|
| `tia_preview_block_change` | Previews proposed changes | R2 |
| `tia_apply_approved_block_change` | Applies approved changes | R3 |

## Manual Test Procedure

### Prerequisites

1. Build the solution: `dotnet build TiaAgent.sln`
2. Start the MCP server: `dotnet run --project src/TiaAgent.McpHost`
3. Start OpenCode (external process, configured to discover MCP at `http://127.0.0.1:43121/mcp`)

### Test A: Add-In → OpenCode

**Command:** "Test OpenCode connection"

**Expected:** Response contains `OPENCODE_CONNECTION_OK`

**Validates:** Add-In can reach OpenCode, create a session, start a task, and receive a response.

### Test B: OpenCode → MCP (Roundtrip)

**Command:** "Test MCP roundtrip"

**Expected:** Response contains `TIA_MCP_ROUNDTRIP_OK` and proves `tia_ping` was called (not fabricated by model).

**Validates:** OpenCode discovers MCP server, calls `tia_ping`, receives real TIA data, returns result.

### Test C: Current TIA Context

**Command:** Via MCP — OpenCode calls `tia_get_current_context`

**Expected:** Returned data matches active TIA Portal instance (project name, version, PLC count).

**Validates:** MCP tool correctly delegates to `ITiaProjectService`.

### Test D: Captured Selection

**Command:** Select a PLC block, trigger "Explain selected object"

**Expected:** Task uses the object captured at command trigger time, not the current visual selection.

**Validation:** Change the visual selection while the task is running. The task must still use the original captured object.

### Test E: Read Selected Block

**Command:** OpenCode calls `tia_read_block(objectId)` via MCP

**Expected:** Result contains real block source code from the active project.

**Validates:** The data comes from the actual project, not fixtures or mocks.

### Test F: Final Response in TIA Portal

**Command:** "Explain selected object" on FB_Conveyor

**Expected:** Add-In UI shows:
- Task status (running → completed)
- Selected object metadata
- Final explanation from OpenCode
- Error details when applicable
- Retry/cancel options

### Test G: TIA Portal Responsiveness

**Command:** Run a deliberately delayed OpenCode operation

**Expected:**
- TIA Portal does not freeze
- Add-In remains responsive
- Cancellation works
- Timeout works
- Second conflicting command is rejected or queued

### Test H: Failure Scenarios

| Scenario | Expected Behavior |
|---|---|
| OpenCode not running | `OPENCODE_UNAVAILABLE` error with user-friendly message |
| Incorrect OpenCode port | `OPENCODE_UNAVAILABLE` error |
| MCP server unavailable | OpenCode reports tool discovery failure |
| No TIA project open | `TIA_PROJECT_NOT_OPEN` from MCP tools |
| Unsupported selection | `TIA_SELECTION_NOT_SUPPORTED` error |
| Selection expired | `TIA_SELECTION_EXPIRED` error |
| Tool timeout | `TIA_TIMEOUT` error |
| Malformed OpenCode response | `OPENCODE_RESPONSE_INVALID` error |

## Error Codes

| Code | Description | User Message |
|---|---|---|
| `OPENCODE_UNAVAILABLE` | OpenCode server not reachable | "AI assistant not available. Ensure OpenCode is running." |
| `OPENCODE_TASK_FAILED` | Task execution failed | "AI assistant encountered an error. Please try again." |
| `TIA_NOT_CONNECTED` | TIA Portal not connected | "Not connected to TIA Portal." |
| `TIA_PROJECT_NOT_OPEN` | No project open | "No TIA Portal project is open." |
| `TIA_SESSION_EXPIRED` | Session expired | "TIA session expired. Please reconnect." |
| `TIA_SELECTION_EXPIRED` | Selection token expired | "Selection expired. Please re-select." |
| `TIA_OBJECT_NOT_FOUND` | Object not found | "Selected object not found." |
| `TIA_TIMEOUT` | Operation timed out | "Operation timed out. Please try again." |
| `TIA_CANCELLED` | Operation cancelled | "Operation was cancelled." |

## Correlation ID Propagation

Every command generates a correlation ID that flows through:

```text
Add-In command → OpenCode session/task → MCP tool calls → ITiaProjectService → response
```

Format: `corr-<guid>` or `tia-<short-guid>`

The correlation ID appears in:
- Structured log entries
- Error responses
- Audit events

## Automated Tests

Run all tests:

```bash
dotnet test TiaAgent.sln
```

Test coverage includes:
- MCP tool handler behavior (TiaContextTools, TiaReadTools, TiaDiagnosticTools)
- OpenCode orchestrator (session creation, task execution, cancellation, error handling)
- Selection snapshot store (save, get, expire, session-scoped expiration)
- Correlation context (async propagation, nested scopes)
- Simulator integration (full explain-block flow, error scenarios)
- Architecture constraints (no Siemens references in wrong projects)
- DTO serialization
- Error code mapping

## Architecture Decisions

1. **OpenCode as external process** — The Add-In connects to an already-running OpenCode server. It does not start OpenCode.

2. **MCP server as separate process** — McpHost runs as a standalone ASP.NET Core app on port 43121. Avoids net48/net8.0 framework conflicts.

3. **Simulator for initial validation** — Uses `SimulatorTiaProjectService` with DemoConveyorLine data. Swap to `TiaAgent.Openness.TiaProjectService` when TIA Portal is available.

4. **Async-first in Add-In** — All OpenCode/MCP communication uses async/await. No `GetAwaiter().GetResult()`.

5. **Selection token pattern** — Selections are captured at command trigger time as immutable snapshots, identified by tokens. The actual block content is fetched through MCP when OpenCode needs it.

## Known Limitations

1. **OpenCode API endpoints assumed** — The `OpenCodeHttpClient` uses a conventional REST API pattern. Verify against the actual OpenCode API.

2. **Simulator data only** — The MCP server currently uses `SimulatorTiaProjectService`. Real TIA Portal integration requires the Openness adapter.

3. **No authentication** — MCP server does not enforce authentication in the current configuration.

4. **In-memory stores** — Selection snapshots and session data are in-memory only. Not persisted across restarts.

## Recommended Next Steps

1. **Verify OpenCode API** — Confirm the actual HTTP endpoints match `OpenCodeHttpClient`.
2. **Add Openness adapter** — Implement `TiaAgent.Openness.TiaProjectService` for real TIA Portal.
3. **Add authentication** — Implement MCP token-based authentication.
4. **Add write operations** — After read-only flow is stable, implement the controlled write workflow.
5. **Add WPF UI** — Replace `MessageBox.Show()` with a proper WPF panel in the Add-In.
