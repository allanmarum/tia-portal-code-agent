# TIA Portal Code Agent

An AI-powered engineering assistant integrated into Siemens TIA Portal through an Add-In, enabling contextual explanations, code review, dependency analysis, and controlled change proposals — all driven by a coding-agent runtime via the Model Context Protocol (MCP).

> **Status:** Functional. Add-In loads in TIA Portal V21, Bridge architecture implemented, context menus work, MCP server connects via Bridge.

## Table of Contents

- [Problem](#problem)
- [Solution](#solution)
- [Architecture](#architecture)
- [Target Environment](#target-environment)
- [Use Cases](#use-cases)
- [MVP Scope](#mvp-scope)
- [Non-Goals](#non-goals)
- [Security Model](#security-model)
- [Repository Structure](#repository-structure)
- [Getting Started](#getting-started)
- [Development](#development)
- [Testing](#testing)
- [Packaging and Installation](#packaging-and-installation)
- [Known Unknowns](#known-unknowns)
- [Contributing](#contributing)
- [License](#license)

## Problem

Engineering work in TIA Portal involves repeated navigation, inspection, documentation, review, reference tracing, and validation tasks. TIA Portal Openness can automate supported engineering functions, but an external tool lacks the exact user context that exists inside the TIA Portal UI.

## Solution

The product combines three roles into a single experience:

| Component | Role |
|---|---|
| **TIA Portal Add-In** | Eyes, hands, and trigger inside TIA — captures context, displays results, controls approvals |
| **Czarnak/tia-portal-mcp** | External MCP server — standardized contract for TIA capabilities via batch read/write tools |
| **OpenCode/MiMoCode (Agent Runtime)** | Brain — session management, planning, model integration, tool calling |

The user invokes an action from the TIA Portal context menu. The Add-In captures the selected object, starts a task in the agent runtime, and the agent uses the external MCP server to read, analyze, and (with approval) modify the project.

## Architecture

```
User → TIA Portal → Add-In → TiaAgent.Bridge → OpenCode Agent → stdio MCP → Czarnak tia-mcp → OpennessWorker → TIA Portal Openness
```

### Layers

| Project | Responsibility |
|---|---|
| `TiaAgent.AddIn` | Contextual commands, selection capture, result/progress UI, Bridge client |
| `TiaAgent.Bridge` | Local HTTP API, task/session management, OpenCode client, process management |
| `TiaAgent.Contracts` | Stable DTOs, interfaces, error codes, events — no Siemens types |
| `TiaAgent.OpenCode` | HTTP client for OpenCode/MiMoCode agent runtime (used by Bridge only) |

### External Components (not in this repo)

| Component | Source | Role |
|---|---|---|
| **Czarnak/tia-portal-mcp** | [GitHub](https://github.com/Czarnak/tia-portal-mcp) | MCP server — TIA Portal access via stdio transport |
| **OpenCode/MiMoCode** | Agent runtime | AI model interaction, tool-calling loop |

### MCP Server (Czarnak/tia-portal-mcp)

Install as a .NET global tool:

```powershell
dotnet tool install -g TiaMcpServer
tia-mcp doctor  # Validate environment
```

The MCP server is spawned automatically by OpenCode via stdio transport. No separate process management needed. It exposes:

- **`execute_read_batch`** — up to 50 read operations per call (browse_project_tree, get_block_content, list_tag_tables, read_hardware_config, read_cross_references, search_equipment_catalog, compile_check, get_project_status)
- **`preview_write_batch`** / **`apply_write_batch`** — safety-token-based writes
- **Project lifecycle tools** — open_project, save_project, close_project, archive_project, create_project, save_project_as

## Target Environment

| Property | Value |
|---|---|
| TIA Portal version | V21 |
| .NET Framework | 4.8 |
| Platform | x64 only |
| Language | C# |
| Add-In type | Class library (modular V21 assemblies) |
| MCP Server | Czarnak/tia-portal-mcp (.NET 8, stdio transport) |
| Development IDE | Visual Studio 2022 (with V21 Siemens extension) |
| Packaging | `Siemens.Engineering.AddIn.Publisher.exe` |
| Installation | User Add-Ins (`%APPDATA%\Siemens\Automation\Portal V21\UserAddIns`) |

**Important:** V21 uses modular assemblies. Do not reference the removed monolithic `Siemens.Engineering.AddIn.dll` or the old `PublicAPI\V21.AddIn` path.

## Use Cases

| ID | Use Case | Description |
|---|---|---|
| UC-001 | Explain selected object | Capture selection, retrieve metadata and content, display an explanation |
| UC-002 | Review selected code | Read object and dependencies, report defects and improvement proposals |
| UC-003 | Trace references | Find origins, usages, dependencies, and call hierarchies |
| UC-004 | Explain compilation messages | Interpret compile diagnostics for selected containers or objects |
| UC-005 | Preview a change | Produce a deterministic change set and diff without applying it |
| UC-006 | Apply approved change | Verify version, save state, apply scoped change, validate, and record |

## MVP Scope

The MVP is **read-only**. No writes, no PLC downloads, no safety or hardware changes.

### Required Commands

- Get active project summary
- Capture selected object
- Read supported block metadata and representation
- Explain selected object
- Show agent progress and final response
- Cancel the task
- Return structured unsupported-operation errors

### MCP Tools (Read-Only Phase)

Via Czarnak's `execute_read_batch`:

```
browse_project_tree       read_hardware_config
get_block_content         read_cross_references
list_tag_tables           search_equipment_catalog
compile_check             get_project_status
```

## Non-Goals

The following are explicitly out of scope for the MVP:

- PLC download
- Online monitoring or control
- Equipment start or stop
- Safety program modification
- Arbitrary hardware or network topology changes
- Unattended project-wide refactoring
- Generic arbitrary Openness execution
- Cloud exposure of the local MCP endpoint

## Security Model

### Trust Boundaries

```
TIA Portal process (Add-In)               — trusted local boundary
TiaAgent.Bridge (port 43119)              — local HTTP, loopback only, bearer token auth
OpenCode agent runtime (port 43120)       — local HTTP, loopback only
Czarnak tia-mcp (stdio)                   — child process, no network
```

### Key Principles

- **Least privilege:** `TIA.ReadOnly` for MVP. Write permissions added only when implemented and reviewed.
- **Loopback only:** OpenCode HTTP endpoint binds to `127.0.0.1`. MCP uses stdio (no network).
- **Prompt injection defense:** Project content (comments, names, source text) is treated as untrusted data.
- **Safety tokens:** Czarnak's preview-then-apply pattern with single-use, expiring tokens bound to exact content.

### Risk Classification

| Class | Operations | Default Policy |
|---|---|---|
| R0 | Context and metadata | Allow |
| R1 | Code and reference reading | Allow + audit |
| R2 | Export, analysis, preview | Allow + audit |
| R3 | Compilation and local validation | Ask |
| R4 | Create, import, modify, rename | Approval token required |
| R5 | Delete, hardware, networks, safety, download | Deny in MVP |

## Repository Structure

```
tia-portal-code-agent/
├── AGENTS.md                      # Agent instruction file
├── README.md
├── TiaAgent.sln                   # Solution file
├── build.ps1                      # Build/test/pack orchestrator
├── src/
│   ├── TiaAgent.AddIn/            # TIA Portal Add-In (UI, commands, Bridge client)
│   ├── TiaAgent.Bridge/           # Local HTTP API, task/session management, OpenCode client
│   ├── TiaAgent.Contracts/        # DTOs, interfaces, errors, events
│   └── TiaAgent.OpenCode/         # OpenCode HTTP client
├── tests/
│   ├── TiaAgent.Application.Tests/
│   ├── TiaAgent.Contracts.Tests/
│   └── TiaAgent.ArchitectureTests/
├── agents/                        # Agent profile definitions
│   ├── tia-explain.md             # Read-only explanation agent
│   ├── tia-review.md              # Review agent (reads + compile)
│   └── tia-change.md              # Change agent (reads + preview + apply)
├── config/
│   ├── opencode.json              # OpenCode + MCP server config
│   ├── opencode.example.json
│   └── bridge.example.json        # Bridge configuration example
└── docs/
    ├── spec/                      # Authoritative specifications
    │   ├── ARCHITECTURE.md
    │   ├── ADDIN_TECHNICAL_SPEC.md
    │   ├── PRODUCT_SPEC.md
    │   ├── SECURITY_MODEL.md
    │   └── KNOWN_UNKNOWNS.md
    └── integration/
        └── opencode-tia-roundtrip.md
```

## Getting Started

### Prerequisites

- **Windows 10/11** (64-bit)
- **Siemens TIA Portal V21** installed with Openness feature
- **.NET SDK 8.0+** (for building and installing MCP server)
- **.NET Framework 4.8 Developer Pack**
- User must be a member of the **Siemens TIA Openness** Windows group

### Install MCP Server

```powershell
dotnet tool install -g TiaMcpServer
tia-mcp doctor  # Validate environment
```

### Build the Add-In

```powershell
.\build.ps1 all    # Build + Test + Pack
.\build.ps1 install  # Copy to UserAddIns
```

### Configure OpenCode

The `config/opencode.json` file configures OpenCode to use Czarnak's MCP server via stdio:

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

### Test the MCP Server

```powershell
# Validate environment
tia-mcp doctor

# Test with MCP Inspector
npx -y @modelcontextprotocol/inspector tia-mcp

# Call a tool
npx -y @modelcontextprotocol/inspector --cli tia-mcp --method tools/call --tool-name execute_read_batch --tool-arg operations='[{"operationId":"status","operation":"get_project_status"}]'
```

## Development

### Solution Layout

The solution contains 4 source projects and 3 test projects (14 architecture tests). MCP and Openness access are delegated to Czarnak's `TiaMcpServer` — this repo contains only the Add-In, Bridge, and OpenCode client.

### Key Rules

- **No engineering objects in fields.** Re-resolve `IEngineeringObject` on every operation.
- **All operations accept `CancellationToken`.** Propagate cancellation and timeout. Never block the TIA UI thread.
- **All errors are structured.** Use the error codes defined in `ARCHITECTURE.md` §17.
- **Every task carries a `correlationId`.** All logs for one operation share the same ID.
- **MCP and Openness are external.** Do not duplicate TIA access — use Czarnak's server.

### Agent Profiles

Agent profiles in `agents/` define the behavior for each command. They use Czarnak's batch API:

| Profile | File | Allowed Tools |
|---|---|---|
| tia-explain | `agents/tia-explain.md` | `execute_read_batch`, `get_project_status` |
| tia-review | `agents/tia-review.md` | `execute_read_batch`, `get_project_status` |
| tia-change | `agents/tia-change.md` | `execute_read_batch`, `get_project_status`, `preview_write_batch`, `apply_write_batch` |

## Testing

### Test Categories

| Category | Scope | TIA Required |
|---|---|---|
| Unit | DTO serialization, error codes, orchestrator logic | No |
| Architecture | Dependency boundaries, no Siemens refs in wrong projects | No |
| MCP Integration | Use `tia-mcp doctor` and MCP Inspector | Yes |

### Running Tests

```powershell
dotnet test TiaAgent.sln
```

## Packaging and Installation

### Publisher

Packaging uses the V21 `Siemens.Engineering.AddIn.Publisher.exe`:

```powershell
.\build.ps1 pack
```

**Never use `--skipEngMemberCheck` in CI or release builds.**

### Installation

1. Build the `.addin` package: `.\build.ps1 pack`
2. Install: `.\build.ps1 install`
3. Open TIA Portal V21 → Add-Ins task card → review permissions → activate.
4. Right-click a PLC block → AI Assistant → Explain/Review/Propose.

## Known Unknowns

Open questions that must be resolved with evidence before implementation:

| ID | Question | Status |
|---|---|---|
| KU-001 | Exact supported TIA Portal V21 build and PublicAPI layout | Partially answered |
| KU-002 | Which Siemens object types support block-level context commands | Open |
| KU-003 | Whether block source can be read directly or requires export | Open |
| KU-004 | Reference/symbol-usage API availability for required object types | Open |
| KU-005 | TIA API threading model | Partially answered |
| KU-006 | Preferred Add-In UI model | Partially answered |
| KU-007 | OpenCode server API version/schema | Resolved — uses HTTP API at port 43120 |
| KU-008 | MCP transport in Add-In process | Resolved — Czarnak's tia-mcp uses stdio, separate process |
| KU-009 | Whether installation requires admin privileges | Open |
| KU-010 | Digital signing requirements | Open |
| KU-011 | Compilation side effects | Open |
| KU-012 | Write recovery | Open |

See `docs/spec/KNOWN_UNKNOWNS.md` for evidence requirements and resolution process.

## Contributing

See `AGENTS.md` for agent-specific guidance. The specs in `docs/spec/` are the source of truth — if code conflicts with specs, the spec wins until a decision updates it.

### Before Editing

1. Read the relevant spec file.
2. Identify the current implementation phase.
3. Locate the responsible layer.
4. Check which invariants are affected.

### Before Completing

1. Build passes (`dotnet build TiaAgent.sln`).
2. Relevant tests pass (`dotnet test TiaAgent.sln`).
3. No dependency cycles introduced.
4. No direct Openness access — use Czarnak's MCP server.
5. No write bypasses approval.
6. No secrets in code or logs.

## License

[Add license here]
