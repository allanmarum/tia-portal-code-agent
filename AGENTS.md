# AGENTS.md

## What this repo is

TIA Portal Code Agent — a Siemens TIA Portal V21 Add-In that integrates an AI agent (via MCP) to assist with PLC engineering tasks. Currently in spec phase; no source code exists yet.

## Non-negotiable constraints

- **Target:** TIA Portal V21, .NET Framework 4.8, x64 only. Do not retarget to modern .NET.
- **Assembly model:** V21 modular assemblies. Do NOT reference the removed monolithic `Siemens.Engineering.AddIn.dll` or the old `PublicAPI\V21.AddIn` path.
- **Single Openness implementation:** All TIA project access goes through `ITiaProjectService` in `TiaAgent.Openness`. No duplicate Openness access from MCP handlers, UI, HTTP clients, or agents.
- **MVP is read-only.** No writes, no PLC download, no safety/hardware/network changes.
- **No Siemens binaries in source control.** References resolved from installed TIA at build time.
- **No `--skipEngMemberCheck`** in CI or release builds.
- **User Add-Ins install to:** `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns`

## Architecture at a glance

```text
Add-In (UI + context capture)
  → Application Services (business rules, validation)
    → ITiaProjectService (single Openness adapter)
      → TIA Portal Openness SDK

MCP tool handlers → Application Services (thin adapters, no direct Openness)
OpenCode → MCP → Add-In (agent runtime, model access)
```

Dependency direction is strict: AddIn → Application → Contracts; Openness → Application + Contracts; Contracts → no Siemens references.

See `docs/spec/ARCHITECTURE.md` (English) and `docs/spec/ADDIN_TECHNICAL_SPEC.md` (English) for the full contract.

## MCP tool conventions

- Names start with `tia_`, express a specific action, separate read/validate/write.
- Write tools include `approved` in the name (e.g. `tia_apply_approved_block_change`).
- Never create generic tools like `tia_execute_arbitrary_openness_operation`.
- Risk classes R0–R2: allow. R3: ask. R4: approval token required. R5: deny in MVP.

## Write flow (when implemented later)

Read snapshot → propose → preview → diff → user approval → validate token + hash → backup → apply → compile → report. No step may be skipped.

## Key spec files

| File | Content |
|---|---|
| `docs/spec/ARCHITECTURE.md` | Architecture contract, dependency rules, tool catalog, flows (en-US) |
| `docs/spec/ADDIN_TECHNICAL_SPEC.md` | V21 Add-In implementation baseline, packaging, installation (English) |
| `docs/spec/PRODUCT_SPEC.md` | Product scope, use cases, MVP requirements |
| `docs/spec/SECURITY_MODEL.md` | Trust boundaries, permissions, prompt injection defense |
| `docs/spec/KNOWN_UNKNOWNS.md` | Open questions requiring evidence before implementation |

## Working in this repo

- **Specs are the source of truth.** If code conflicts with specs, the spec wins until a decision updates it.
- **No build/test tooling exists yet.** When it does, expect: `dotnet build`, `dotnet test`, Publisher packaging via `Siemens.Engineering.AddIn.Publisher.exe`.
- **Engineering objects are local-scope only.** Never store `IEngineeringObject` in fields, properties, statics, caches, or DI singletons. Re-resolve on every operation.
- **All operations need `CancellationToken`.** All errors must be structured (see error codes in ARCHITECTURE.md §17).
- **Every task needs a `correlationId`** for traceability.
