# TIA Portal Code Agent

An AI-powered engineering assistant integrated into Siemens TIA Portal through an Add-In, enabling contextual explanations, code review, dependency analysis, and controlled change proposals — all driven by a coding-agent runtime via the Model Context Protocol (MCP).

> **Status:** Early implementation phase. Simulator and MCP tools are functional; Openness adapter is a placeholder.

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
| **MCP Interface** | Standardized contract for TIA capabilities — narrow, typed, auditable tools |
| **OpenCode (Agent Runtime)** | Brain — session management, planning, model integration, tool calling |

The user invokes an action from the TIA Portal context menu. The Add-In captures an immutable selection snapshot, starts a task in the agent runtime, and the agent uses structured MCP tools to read, analyze, and (with approval) modify the project.

## Architecture

```
User → TIA Portal → Add-In → OpenCode Agent → MCP Tools → Application Services → ITiaProjectService → TIA Portal Openness
```

### Dependency Graph

```
AddIn → Application → Contracts
Openness → Application + Contracts
MCP → Application + Contracts
Contracts → (no Siemens references)
```

### Layers

| Project | Responsibility |
|---|---|
| `TiaAgent.AddIn` | Contextual commands, selection capture, result/progress UI, lifecycle management |
| `TiaAgent.Application` | Business rules, validation, change policies, approval logic |
| `TiaAgent.Openness` | Single TIA Portal Openness adapter — project navigation, read/write, compilation, DTO mapping |
| `TiaAgent.Contracts` | Stable DTOs, requests, responses, error codes, events — no Siemens types |
| `TiaAgent.Mcp` | MCP tool handlers (thin adapters), authentication, policy enforcement |
| `TiaAgent.OpenCode` | Agent runtime client — session management, task lifecycle, event monitoring |

### Single Openness Rule

All TIA project access flows through one implementation of `ITiaProjectService`. No duplicate Openness access from MCP handlers, UI, HTTP clients, or agents. Engineering objects are local-scope only — never stored in fields, properties, statics, caches, or DI singletons.

## Target Environment

| Property | Value |
|---|---|
| TIA Portal version | V21 |
| .NET Framework | 4.8 |
| Platform | x64 only |
| Language | C# |
| Add-In type | Class library (modular V21 assemblies) |
| Development IDE | Visual Studio 2022 (with V21 Siemens extension) |
| Packaging | `Siemens.Engineering.AddIn.Publisher.exe` |
| Installation | User Add-Ins (`%APPDATA%\Siemens\Automation\Portal V21\UserAddIns`) |

**Important:** V21 uses modular assemblies. Do not reference the removed monolithic `Siemens.Engineering.AddIn.dll` or the old `PublicAPI\V21.AddIn` path. V21 Add-Ins are not backward-compatible with earlier TIA versions.

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

### Optional MVP Commands

- List blocks with pagination
- Get block interface
- Get direct dependencies
- Open a referenced object in TIA

### MCP Tools (Read-Only Phase)

```
tia_get_current_context    tia_list_blocks
tia_get_selection          tia_read_block
tia_get_project_summary    tia_get_block_interface
tia_list_devices           tia_find_references
tia_list_plcs              tia_get_call_hierarchy
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
- Universal TIA version support
- Cloud exposure of the local MCP endpoint
- Permanent full-project indexing
- Cloud synchronization of PLC source code

## Security Model

### Trust Boundaries

```
TIA Portal process (Add-In + Application + Openness)  — trusted local boundary
Loopback HTTP / Named Pipe                              — authenticated local transport boundary
Agent runtime and model                                 — untrusted reasoning boundary
Project content                                         — untrusted data boundary
```

### Key Principles

- **Least privilege:** `TIA.ReadOnly` for MVP. Write permissions added only when implemented and reviewed.
- **Loopback only:** HTTP endpoints bind to `127.0.0.1`. Named pipes with user ACLs preferred.
- **Prompt injection defense:** Project content (comments, names, source text) is treated as untrusted data — it cannot grant permissions, approve changes, or alter tool policy.
- **No secrets in packages:** API keys and model provider credentials stay outside the Add-In.
- **Approval tokens:** Single-use, short-lived, bound to exact content hash, object scope, and session. Generated outside the model context, never accepted from chat alone.

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
│   ├── TiaAgent.AddIn/            # TIA Portal Add-In (UI, commands, bootstrap)
│   ├── TiaAgent.Application/      # Business rules, validation, policies
│   ├── TiaAgent.Openness/         # Single Openness adapter (placeholder)
│   ├── TiaAgent.Contracts/        # DTOs, requests, responses, errors, events
│   ├── TiaAgent.Mcp/              # MCP tool handlers and DI registration
│   ├── TiaAgent.McpHost/          # Standalone MCP host process
│   ├── TiaAgent.Simulator/        # In-memory ITiaProjectService for dev/test
│   └── TiaAgent.OpenCode/         # Agent runtime client
├── tests/
│   ├── TiaAgent.Application.Tests/
│   ├── TiaAgent.Contracts.Tests/
│   ├── TiaAgent.Mcp.Tests/
│   ├── TiaAgent.ArchitectureTests/
│   └── TiaAgent.IntegrationTests/
├── agents/                        # Agent profile definitions
│   ├── tia-explain.md
│   ├── tia-review.md
│   └── tia-change.md
├── config/
│   ├── opencode.example.json
│   ├── appsettings.example.json
│   └── capabilities.example.json
├── scripts/                       # Development and CI scripts
└── docs/
    └── spec/                      # Authoritative specifications
        ├── ARCHITECTURE.md        # Architecture contract (en-US)
        ├── ADDIN_TECHNICAL_SPEC.md # V21 Add-In baseline (English)
        ├── PRODUCT_SPEC.md        # Product scope and requirements
        ├── SECURITY_MODEL.md      # Trust boundaries and permissions
        └── KNOWN_UNKNOWNS.md      # Open questions requiring evidence
```

## Getting Started

### Prerequisites

- **Windows 10/11** (64-bit)
- **Siemens TIA Portal V21** installed with Openness feature
- **Visual Studio 2022** with the V21 Add-In Development Tools VSIX extension
- **.NET Framework 4.8 Developer Pack**
- User must be a member of the **Siemens TIA Openness** Windows group

### Openness Group Membership

TIA Portal Openness access is controlled by the local `Siemens TIA Openness` group:

1. An administrator adds the user to the group.
2. The user fully signs out and signs back in.
3. Missing membership causes `EngineeringSecurityException` when accessing TIA through Openness.

### PublicAPI Path (V21)

```
C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\
```

Build tooling discovers this path from registry metadata or a repository property — do not hard-code it.

## Development

### Solution Layout

The solution follows a strict dependency graph. Only `TiaAgent.Openness` and `TiaAgent.AddIn` may reference Siemens assemblies. `TiaAgent.Contracts` must be free of Siemens types.

### Key Rules

- **No engineering objects in fields.** Re-resolve `IEngineeringObject` on every operation. V21 Add-Ins remain loaded for performance, making stale references dangerous.
- **All operations accept `CancellationToken`.** Propagate cancellation and timeout. Never block the TIA UI thread.
- **All errors are structured.** Use the error codes defined in `ARCHITECTURE.md` §17 (e.g. `TIA_OBJECT_CHANGED`, `TIA_SESSION_EXPIRED`, `APPROVAL_REQUIRED`).
- **Every task carries a `correlationId`.** All logs for one operation share the same ID.
- **MCP handlers are thin adapters.** Delegate to Application Services — never access Openness directly.

### Implementation Phases

| Phase | Deliverable | AI Involved |
|---|---|---|
| 0 — Openness proof | Context, selection, block read, compile, DTO, non-blocking dispatcher | No |
| 1 — Read-only agent | `tia_get_*`, `tia_read_block`, explain command, cancellation | Yes |
| 2 — Navigation & dependencies | `tia_list_blocks`, call hierarchy, references, pagination, cache | Yes |
| 3 — Review & preview | Compile, validate change, preview with diff (no apply) | Yes |
| 4 — Approved writes | Approval tokens, hash validation, backup, apply, compile, audit | Yes |
| 5 — Isolation | Extract MCP Host to external process with IPC | Yes |

## Testing

### Test Categories

| Category | Scope | TIA Required |
|---|---|---|
| Unit | Validation, risk policy, token lifecycle, error mapping, pagination | No |
| Contract | DTO serialization, MCP tool schemas, IPC messages, error codes | No |
| Integration (no TIA) | Tool calling, auth, permissions, preview/approval flow with `ITiaProjectService` fake | No |
| Integration (TIA) | Session detection, selection capture, block read, compile, write round-trip | Yes |
| Migration | V20-to-V21 behavioral comparison, namespace/type changes | Yes |

### Negative Test Cases (Required)

- Project closed during operation
- Object deleted between capture and execution
- Selection expired
- Token expired or reused
- Hash mismatch on write
- Unsupported operation
- Compilation failure
- Cancellation
- Timeout
- Excessive payload
- Unauthenticated call
- Write attempt without approval
- Object outside approval scope
- Project content containing prompt injection

## Packaging and Installation

### Publisher

Packaging uses the V21 `Siemens.Engineering.AddIn.Publisher.exe`:

```powershell
& $publisher `
  --configuration $configPath `
  --outfile $outputAddIn `
  --logfile $publisherLog `
  --verbose `
  --console
```

**Never use `--skipEngMemberCheck` in CI or release builds.**

### Config.xml

The package configuration uses the V21 Publisher schema:

```xml
<PackageConfiguration
  xmlns="http://www.siemens.com/Automation/Openness/AddIn/Publisher/V21">
```

Generate the actual file from the installed V21 template and validate with the Publisher.

### Installation

1. Build the `.addin` package via Publisher.
2. Copy to `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns` (or use the TIA Add-Ins task card).
3. Open TIA Portal V21 → Add-Ins task card → review permissions → activate.
4. Invoke the command from a supported context-menu object.

### Artifact Naming

```
TiaAgent-v1.0.0-tia-v21.addin
```

### Packaging Checklist

- Targets `net48` and `x64`
- No `Siemens.Engineering.AddIn.dll` reference
- No old `V21.AddIn` path
- Publisher runs without `--skipEngMemberCheck`
- Generated `.addin` exists and is non-empty
- No Siemens runtime DLLs shipped in the package
- SHA-256 hash computed and published

## Known Unknowns

Open questions that must be resolved with evidence before implementation:

| ID | Question |
|---|---|
| KU-001 | Exact supported TIA Portal V21 build and PublicAPI layout |
| KU-002 | Which Siemens object types support block-level context commands |
| KU-003 | Whether block source can be read directly or requires export |
| KU-004 | Reference/symbol-usage API availability for required object types |
| KU-005 | TIA API threading model — which operations on which context |
| KU-006 | Preferred Add-In UI model (embedded panel vs. WPF window) |
| KU-007 | Installed OpenCode version and endpoint schema |
| KU-008 | MCP C# SDK compatibility with .NET Framework 4.8 |
| KU-009 | Whether installation requires admin privileges in target policy |
| KU-010 | Digital signing requirements for deployment |
| KU-011 | Compilation side effects in target TIA version |
| KU-012 | Which object types support reliable export/import rollback |

See `docs/spec/KNOWN_UNKNOWNS.md` for evidence requirements and resolution process.

## Error Codes

The system uses structured error codes. The full set is defined in `ARCHITECTURE.md` §17. Key codes:

| Code | Meaning |
|---|---|
| `TIA_NOT_CONNECTED` | No TIA Portal process available |
| `TIA_PROJECT_NOT_OPEN` | No project loaded |
| `TIA_SESSION_EXPIRED` | Session no longer valid |
| `TIA_OBJECT_NOT_FOUND` | Engineering object missing |
| `TIA_OBJECT_CHANGED` | Object modified since snapshot |
| `TIA_OPERATION_NOT_SUPPORTED` | Capability unavailable in this version |
| `TIA_COMPILE_FAILED` | Compilation returned errors |
| `TIA_TIMEOUT` | Operation exceeded time limit |
| `TIA_CANCELLED` | Operation was cancelled |
| `APPROVAL_REQUIRED` | Write requires user approval |
| `APPROVAL_EXPIRED` | Approval token expired |
| `APPROVAL_DIFF_MISMATCH` | Content changed since approval |

## Contributing

See `AGENTS.md` for agent-specific guidance. The specs in `docs/spec/` are the source of truth — if code conflicts with specs, the spec wins until a decision updates it.

### Before Editing

1. Read the relevant spec file.
2. Identify the current implementation phase.
3. Locate the responsible layer.
4. Check which invariants are affected.

### Before Completing

1. Build passes.
2. Relevant tests pass.
3. No dependency cycles introduced.
4. No direct Openness access outside `TiaAgent.Openness`.
5. No write bypasses approval.
6. No service listens outside loopback.
7. No secrets in code or logs.

## License

[Add license here]
