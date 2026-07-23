# AGENTS.md

## What this repo is

TIA Portal Code Agent — a Siemens TIA Portal V21 Add-In that integrates an AI agent (via MCP) to assist with PLC engineering tasks.

## Non-negotiable constraints

- **Target:** TIA Portal V21, .NET Framework 4.8, x64 only. Do not retarget to modern .NET.
- **Assembly model:** V21 modular assemblies. Do NOT reference the removed monolithic `Siemens.Engineering.AddIn.dll` or the old `PublicAPI\V21.AddIn` path.
- **MCP server:** Uses [Czarnak/tia-portal-mcp](https://github.com/Czarnak/tia-portal-mcp) via stdio transport. Install with `dotnet tool install -g TiaMcpServer`.
- **MVP is read-only.** No writes, no PLC download, no safety/hardware/network changes.
- **No Siemens binaries in source control.** References resolved from installed TIA at build time.
- **No `--skipEngMemberCheck`** in CI or release builds.
- **User Add-Ins install to:** `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns`

## Architecture at a glance

```text
TIA Portal Add-In (UI + context capture, net48)
  → Runtime Discovery (reads runtime.json)
    → Bridge (HTTP, port 43119)
      → IAgentRuntime (runtime abstraction)
        +--- MimoCliRuntime (mimo run --format json)
        +--- OpenCodeRuntime (server or CLI mode)
        +--- ClaudeCodeRuntime (claude -p --output-format json)
          → Czarnak's tia-mcp (stdio MCP server, .NET 8)
            → OpennessWorker (.NET 4.8)
              → TIA Portal Openness SDK
```

This repo contains: **Add-In**, **Application** (business logic), **Contracts** (DTOs, errors, interfaces, runtime abstraction), **Bridge** (HTTP bridge with runtime adapters), **Runtime Supervisor** (PowerShell scripts for service lifecycle).

MCP and Openness are delegated to Czarnak's `TiaMcpServer` — do not duplicate TIA access.

## Supported runtimes

The Bridge supports multiple interchangeable coding agent runtimes:

| Runtime | ID | Mode | Prerequisites |
|---|---|---|---|
| Mimo CLI | `mimo` | CLI | `mimo` on PATH |
| OpenCode | `opencode` | Server or CLI | `opencode` on PATH |
| Claude Code CLI | `claude` | CLI | `claude` on PATH |

Runtime selection precedence:
1. Runtime explicitly included in the task request
2. `TIA_AGENT_RUNTIME` environment variable
3. User configuration file (`%LOCALAPPDATA%\TiaAgent\config.json`)
4. Configured default (`opencode`)

See `docs/RUNTIME.md` for full runtime configuration details.

## Runtime Supervisor

The Runtime Supervisor orchestrates service startup, monitoring, and shutdown. It is runtime-aware: it reads `%LOCALAPPDATA%\TiaAgent\config.json` to determine which runtime to start, and only launches a server process when the selected runtime is in server mode.

```text
# Start all services
tia-agent start  (or .\src\runtime\Scripts\run.ps1)

# Check status
tia-agent status (or .\src\runtime\Scripts\status.ps1)

# Stop all services
tia-agent stop   (or .\src\runtime\Scripts\stop.ps1)
```

See `docs/RUN.md` for detailed usage, `docs/RUNTIME.md` for runtime configuration, and `docs/spec/ARCHITECTURE.md` for the architectural specification.

See `docs/spec/ARCHITECTURE.md` for the full architecture contract.

## MCP tools (Czarnak/tia-portal-mcp)

The MCP server exposes batch tools:

- **`execute_read_batch`** — up to 50 read operations per call
- **`preview_write_batch`** — preview writes, returns `safetyToken`
- **`apply_write_batch`** — apply previewed writes with `safetyToken`
- **`get_project_status`** — project metadata
- **Project lifecycle:** `open_project`, `save_project`, `close_project`, `archive_project`, `create_project`, `save_project_as`

Read operations: `browse_project_tree`, `get_block_content`, `list_tag_tables`, `read_hardware_config`, `read_cross_references`, `search_equipment_catalog`, `compile_check`, `get_project_status`

## Agent profiles

- `agents/tia-explain.md` — read-only explanation agent
- `agents/tia-review.md` — review agent (reads + compile check)
- `agents/tia-change.md` — change agent (reads + preview + apply with safety tokens)

## Working in this repo

- **Specs are the source of truth.** If code conflicts with specs, the spec wins until a decision updates it.
- **Build:** `dotnet build TiaAgent.sln`
- **Test:** `dotnet test TiaAgent.sln`
- **Engineering objects are local-scope only.** Never store `IEngineeringObject` in fields, properties, statics, caches, or DI singletons. Re-resolve on every operation.
- **All operations need `CancellationToken`.** All errors must be structured (see error codes in ARCHITECTURE.md).
- **Every task needs a `correlationId`** for traceability.

## Serial issue execution

The release roadmap in `.github/serial-roadmap.json` is strictly serial. Only the single item with status `active` may be implemented.

Before starting work:

1. Read `.github/serial-roadmap.json`.
2. Confirm the requested issue number and `REL-XXX` key match the active item.
3. Confirm the predecessor is `done`.
4. Pull the latest `main`.
5. Create `issue/<number>-<sequence-lowercase>-<slug>`.
6. Keep the implementation limited to the active issue.

Do not:

- start, branch, or open a PR for a blocked issue;
- combine multiple roadmap issues in one PR;
- skip or reorder roadmap items;
- change roadmap keys, issue numbers, titles, or order;
- close an issue manually without the implementation PR;
- modify future work unless the active issue explicitly requires it.

Every implementation PR must close exactly the active issue and atomically advance the roadmap by one item. The PR title, body metadata, branch name, and roadmap transition are enforced by `scripts/ci/validate-serial-roadmap.ps1` through the consolidated `ci.yml` workflow.
