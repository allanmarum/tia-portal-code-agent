# Product Specification

Status: draft  
Product name: TIA Portal Code Agent  
Primary environment: Windows + Siemens TIA Portal  
Baseline source version: TIA Portal / Startdrive V16 and V17

## 1. Problem

Engineering work in TIA Portal contains repeated navigation, inspection, documentation, review, reference tracing, and validation tasks.

TIA Portal Openness can automate supported engineering functions, but an external tool usually lacks the exact user context that exists inside the TIA Portal UI.

The product must combine:

- contextual invocation inside TIA Portal;
- controlled access to supported Openness functionality;
- a coding-agent runtime capable of reasoning and tool use;
- human control over engineering changes.

## 2. Product statement

A TIA Portal Add-In provides the user-facing entry point and captures the selected engineering context. A single Openness application service implements all project access. An optional MCP interface exposes approved capabilities to an external agent runtime. The Add-In displays results and controls approvals.

The Runtime Supervisor orchestrates the local development runtime, managing service lifecycle, port allocation, and health monitoring.

## 3. Primary users

### Automation engineer

Needs explanations, dependency analysis, signal tracing, documentation, compile diagnostics, and controlled change proposals.

### Add-In developer

Needs predictable contracts, version isolation, testable services, package generation, and explicit permissions.

### Engineering lead or reviewer

Needs traceability, diffs, risk information, compilation evidence, and proof that no online operation occurred.

## 4. Core use cases

### UC-001 Explain selected object

The user invokes `Explain selected object` from a supported TIA context menu. The system captures an immutable selection token, retrieves supported metadata and content, and displays an explanation.

### UC-002 Review selected code

The user requests a review. The agent reads the selected object and necessary dependencies. It reports defects, assumptions, and improvement proposals without modifying the project.

### UC-003 Trace references

The user requests origin, usage, dependency, or call-hierarchy information. The system uses only supported Openness capabilities or explicitly identified export analysis.

### UC-004 Explain compilation messages

The user selects a supported software container or object and asks for compile-message interpretation. Compilation is a separately permissioned operation.

### UC-005 Preview a change

The agent proposes a change. The system produces a deterministic change set and diff without applying it.

### UC-006 Apply an approved change

A human approves one immutable change set. The system verifies object version, saves a recoverable previous state, applies the scoped change, validates it, and records the result.

## 5. Functional requirements

### Context and selection

- `FR-001`: The Add-In shall expose commands only on supported context types.
- `FR-002`: The Add-In shall capture the object set that initiated the command.
- `FR-003`: The system shall not replace the captured selection with the current visual selection later.
- `FR-004`: Selection tokens shall expire when the TIA session or project is no longer valid.

### Openness access

- `FR-010`: All TIA project operations shall pass through one application service boundary.
- `FR-011`: The Openness adapter shall convert Siemens objects to internal DTOs.
- `FR-012`: The system shall report unsupported operations explicitly.
- `FR-013`: The system shall expose version capabilities.

### Agent runtime

- `FR-020`: The Add-In shall be able to create or submit a task to a configured local agent runtime.
- `FR-021`: Initial context shall be minimal.
- `FR-022`: Additional project data shall be requested through narrow tools.
- `FR-023`: Agent unavailability shall not freeze or crash TIA Portal.

### MCP

- `FR-030`: MCP tools shall be narrow, typed, and auditable.
- `FR-031`: Read and write tools shall be separate.
- `FR-032`: Arbitrary Openness execution shall not be exposed.
- `FR-033`: Tool handlers shall delegate to the same services used by Add-In commands.

### Change control

- `FR-040`: Every write shall require an immutable preview.
- `FR-041`: Approval shall be represented by a scoped, expiring token.
- `FR-042`: The current object hash shall match the preview hash before application.
- `FR-043`: The system shall record before and after hashes.
- `FR-044`: The system shall report partial failure.
- `FR-045`: No write shall imply PLC download.

### Packaging and activation

- `FR-050`: Package metadata shall be versioned.
- `FR-051`: The package shall be produced with the Publisher associated with the selected TIA installation.
- `FR-052`: The package shall request only required permissions.
- `FR-053`: Installation and activation instructions shall be documented per supported TIA version.

## 6. Non-functional requirements

- `NFR-001`: Do not block the TIA UI thread with long operations.
- `NFR-002`: Expected failures use structured error codes.
- `NFR-003`: All operations support correlation IDs.
- `NFR-004`: Write operations are serialized per project.
- `NFR-005`: Local network endpoints bind to loopback.
- `NFR-006`: Secrets are external to the Add-In package.
- `NFR-007`: Domain and application logic are testable without TIA Portal.
- `NFR-008`: Version-dependent behavior is isolated.
- `NFR-009`: Logs omit secrets and source payloads by default.
- `NFR-010`: Cancellation and timeouts are propagated.

## 7. Explicit non-goals for the first product

- PLC download.
- Online monitoring or control.
- Equipment start or stop.
- Safety program modification.
- Arbitrary hardware topology modification.
- Unattended project-wide refactoring.
- Generic execution of arbitrary Openness calls.
- Claiming support for every object visible in TIA Portal.
- Cloud exposure of the local MCP endpoint.

## 8. MVP scope

The MVP is read-only.

Required commands:

- Get active project summary.
- Capture selected object.
- Read supported block metadata.
- Read supported block representation.
- Explain selected object.
- Show agent progress and final response.
- Cancel the task.
- Return structured unsupported-operation errors.

Optional MVP commands:

- List blocks with pagination.
- Get block interface.
- Get direct dependencies.
- Open a referenced object in TIA.

## 9. MVP acceptance criteria

- A supported object exposes an `Explain selected object` context-menu action.
- The Add-In receives the active `TiaPortal` process object through the provider.
- Selection is captured once and represented by internal IDs.
- No Siemens object crosses into MCP or agent-runtime contracts.
- The agent can call at least one read-only `tia_*` tool.
- Failure of the runtime or MCP endpoint is shown as a recoverable error.
- No project write occurs.
- No non-loopback listener is created.
- Build, package, install, activate, and manual execution steps are documented and observed on one supported TIA version.
