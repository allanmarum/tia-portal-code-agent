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
Add-In (UI + context capture, net48)
  → OpenCodeOrchestrator (session/task management)
    → OpenCode/MiMoCode (AI agent runtime, port 43120)
      → Czarnak's tia-mcp (stdio MCP server, .NET 8)
        → OpennessWorker (.NET 4.8)
          → TIA Portal Openness SDK
```

This repo contains: **Add-In**, **Application** (orchestrator), **OpenCode** (HTTP client), **Contracts** (DTOs, errors, interfaces).

MCP and Openness are delegated to Czarnak's `TiaMcpServer` — do not duplicate TIA access.

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
