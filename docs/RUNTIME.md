# Runtime Configuration

The TIA Agent Bridge supports multiple interchangeable coding agent runtimes. The user selects which agent CLI should process TIA Portal tasks without changing the Add-In, MCP contracts, or engineering services.

## Supported runtimes

| Runtime | ID | Mode | Description |
|---|---|---|---|
| Mimo CLI | `mimo` | CLI | Non-interactive execution via `mimo run --format json` |
| OpenCode | `opencode` | Server or CLI | Server mode (default): HTTP API to `opencode serve`. CLI mode: `opencode run` |
| Claude Code CLI | `claude` | CLI | Non-interactive execution via `claude -p --output-format json` |

## Architecture

```text
TIA Portal Add-In
        |  IAgentBridgeClient (HTTP)
        v
TiaAgent Bridge
        |  IAgentRuntime (abstraction)
        +---------------------+
        |                     |
        v                     v
MimoCliRuntime      OpenCodeRuntime    ClaudeCodeRuntime
(Process CLI)       (HTTP or CLI)      (Process CLI)
        |                     |
        | MCP (via agent's own config) |
        v                     v
TiaAgent MCP Server (tia-mcp, unchanged)
        |
        v
TIA Portal Openness (unchanged)
```

The Add-In communicates exclusively with the Bridge. It has no direct dependency on any runtime.

## Configuration file

**Path:** `%LOCALAPPDATA%\TiaAgent\config.json`

```json
{
  "defaultRuntime": "opencode",
  "runtimes": {
    "mimo": {
      "enabled": true,
      "executable": "mimo"
    },
    "opencode": {
      "enabled": true,
      "mode": "server",
      "executable": "opencode",
      "serverUrl": "http://127.0.0.1:43120"
    },
    "claude": {
      "enabled": true,
      "executable": "claude"
    }
  }
}
```

### Fields

- **`defaultRuntime`** — The runtime ID to use when no override is specified. Default: `"opencode"`.
- **`runtimes.<id>.enabled`** — Whether this runtime is available for selection. Default: `true`.
- **`runtimes.<id>.executable`** — Path to the runtime executable. If omitted, the runtime is expected to be on PATH.
- **`runtimes.<id>.mode`** — `"server"` or `"cli"`. Only relevant for OpenCode. Default: `"server"`.
- **`runtimes.<id>.serverUrl`** — Server URL for server mode. Default: `"http://127.0.0.1:43120"`.
- **`runtimes.<id>.environment`** — Additional environment variables for the runtime process.

## Runtime selection precedence

1. **Request override** — The `runtime` field in a task request takes highest precedence.
2. **Environment variable** — `TIA_AGENT_RUNTIME=mimo|opencode|claude`
3. **Configuration file** — `defaultRuntime` in `config.json`
4. **Hardcoded default** — `"opencode"` if nothing else is configured.

There is **no silent fallback**. If the selected runtime is unavailable, the Bridge returns an actionable error:

```text
Selected runtime `mimo` is unavailable.

Executable: mimo
Reason: executable not found
Available runtimes: opencode, claude
```

## CLI vs Server modes

### CLI mode (mimo, claude, opencode with `mode: "cli"`)

The Bridge spawns a process for each task:
- `mimo run "<prompt>" --format json --agent <agentId> --never-ask --trust`
- `claude -p "<prompt>" --output-format json --mcp-config <file> --dangerously-skip-permissions --no-session-persistence`
- `opencode run "<prompt>" --format json --agent <agentId>`

No server process is needed. The runtime supervisor skips starting a server when the configured runtime is in CLI mode.

### Server mode (opencode with `mode: "server"`)

The Bridge communicates with a running OpenCode server via HTTP:
- The runtime supervisor starts `opencode serve --port <port>`
- The Bridge sends requests to the server's API endpoints
- Sessions are managed by the server

## Bridge API endpoints

### Runtime management

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/runtimes` | List all registered runtimes with availability status |
| `GET` | `/api/runtimes/{id}/health` | Check specific runtime health |
| `GET` | `/api/settings/runtime` | Get current default runtime |
| `PUT` | `/api/settings/runtime` | Set default runtime (persists to config.json) |

### Task execution (existing, updated)

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/tasks` | Create a task. Body may include `"runtime": "claude"` to override. |
| `GET` | `/v1/tasks/{taskId}` | Get task status. Response includes `runtimeId` and `runtimeVersion`. |
| `POST` | `/v1/tasks/{taskId}/cancel` | Cancel a task. |

### Task request with runtime override

```json
{
  "action": "explain",
  "runtime": "claude",
  "correlationId": "tia-abc123",
  "agentId": "tia-explain",
  "userMessage": "Explain this OB block"
}
```

The `runtime` property may be omitted to use the configured default.

## Prerequisites

Each runtime requires its CLI to be installed and available on PATH (or configured with an explicit executable path):

- **mimo**: `npm install -g @mimo-ai/cli` (or equivalent)
- **opencode**: `npm install -g opencode` (or equivalent)
- **claude**: `npm install -g @anthropic-ai/claude-code` (or equivalent)

The MCP server (`tia-mcp`) is required for all runtimes. Install with:
```bash
dotnet tool install -g TiaMcpServer
```

## MCP integration

All runtimes receive the same MCP configuration pointing to `tia-mcp`. The MCP tools are identical regardless of which runtime is used:

- Read operations: `browse_project_tree`, `get_block_content`, `list_tag_tables`, etc.
- Write operations: `preview_write_batch`, `apply_write_batch` (with safety tokens)
- Project lifecycle: `open_project`, `save_project`, `close_project`, etc.

For Claude Code, the Bridge generates an MCP configuration file at runtime pointing to `tia-mcp` via stdio transport.

## Startup flow

The runtime supervisor (`run.ps1`) follows this flow:

1. Load `config.json` to determine the selected runtime
2. Start the Bridge (always)
3. If the runtime is in **server mode**: start the runtime server and wait for health
4. If the runtime is in **CLI mode**: verify the executable is available (no server needed)
5. Publish runtime manifest with runtime metadata
6. Monitor processes

## Status file

The runtime manifest (`runtime.json`) includes runtime information:

```json
{
  "schemaVersion": 1,
  "instanceId": "...",
  "status": "ready",
  "runtime": {
    "id": "claude",
    "displayName": "Claude Code CLI",
    "mode": "cli",
    "healthy": true
  },
  "services": {
    "bridge": { "status": "healthy", "port": 43119 },
    "opencode": { "status": "skipped" }
  }
}
```

## Adding a new runtime adapter

To add a new runtime:

1. Create a class implementing `IAgentRuntime` in `src/TiaAgent.Bridge/Runtime/`
2. Register it in `Program.RegisterRuntimes()`
3. Add its ID to `RuntimeConfigLoader.KnownRuntimes`
4. Update the supervisor scripts if it needs a server process
5. Add tests using `FakeRuntime` as a pattern
6. Update this documentation

The runtime adapter must:
- Use `ProcessStartInfo` with `UseShellExecute = false` and redirected streams
- Support cancellation via `CancellationToken`
- Enforce configurable timeouts
- Strip ANSI escape codes at the presentation boundary
- Return structured `AgentTaskResult` with success/failure, response text, and runtime metadata

## Troubleshooting

### Runtime not found

```
Unknown runtime 'xyz'. Available runtimes: mimo, opencode, claude
```

Check that the runtime ID is correct and the adapter is registered in the Bridge.

### Runtime unavailable

```
Selected runtime `mimo` is unavailable.
Executable: mimo
Reason: executable not found
```

Install the runtime CLI or update the `executable` path in `config.json`.

### Server health check failed

The runtime server (OpenCode in server mode) failed to respond to health checks. Check:
- The server is installed (`opencode --version`)
- The port is not in use
- The server logs (`%LOCALAPPDATA%\TiaAgent\logs\opencode.log`)

## Security considerations

- Runtime processes run with the same permissions as the Bridge
- No secrets are passed via command-line arguments when avoidable
- The MCP server uses the same authentication mechanism regardless of runtime
- `--dangerously-skip-permissions` is used for Claude Code in non-interactive mode only
- All runtime communication is local (127.0.0.1)
