# Security and Safety Model

Status: mandatory baseline

## 1. Protected assets

- TIA engineering project integrity.
- PLC and plant operational safety.
- safety-related engineering data.
- source code and intellectual property.
- Windows user credentials.
- model provider credentials.
- local agent-runtime credentials.
- audit history.
- Add-In package integrity.

## 2. Trust boundaries

```text
TIA Portal process
  | Add-In assemblies
  | application services
  | Openness adapter
  +---------------- trusted local engineering boundary

loopback HTTP / named pipe
  +---------------- authenticated local transport boundary

agent runtime and model
  +---------------- untrusted reasoning boundary

project content
  +---------------- untrusted data boundary
```

The model is not authorized merely because it generated a tool call.

## 3. Permission classes

### Read

Examples:

- context;
- object metadata;
- block snapshot;
- interfaces;
- references.

Default: allow when within captured project and payload limits.

### Analysis

Examples:

- export to controlled temporary storage;
- diff;
- dependency graph.

Default: allow with audit and cleanup.

### Validation

Examples:

- compilation;
- consistency check.

Default: explicit policy or user confirmation depending on cost and side effects.

### Write

Examples:

- import;
- create;
- rename;
- modify.

Default: deny unless preview and approval flow is complete.

### Prohibited

- PLC download;
- online control;
- safety modification;
- arbitrary hardware/network changes;
- deletion;
- arbitrary Openness execution.

Default: no public tool.

## 4. `Config.xml` least privilege

The `Config.xml` manifest declares three permission categories. Only the minimum required set should be declared.

### 4.1 `TIAPermissions`

Controls TIA Portal Openness access level:

- `<TIA.ReadWrite />` - full read/write access to the engineering project;
- `<TIA.ReadOnly />` - read-only access.

**MVP policy**: Use `<TIA.ReadOnly />` unless write capability is explicitly required. See `ADDIN_TECHNICAL_SPEC.md` section 4.1.

### 4.2 `SecurityPermissions`

Declares individual .NET Code Access Security (CAS) permissions. Each permission requires an explicit element. Available permissions include process start, file I/O, network, registry, and UI permissions.

### 4.3 `UnrestrictedAccess` (V19+ only)

Grants the Add-In all permissions the current user holds. Requires a justification comment (10-120 characters).

**MVP policy**: Do not use `UnrestrictedAccess`. It conflicts with the least-privilege requirement.

### 4.4 Repository policy

- every requested permission must have a feature ID;
- permissions are reviewed in pull requests;
- unused permissions are removed;
- read-only MVP must not request broad write capability;
- process-start permission is added only if the Add-In itself starts a local runtime;
- network permission is constrained to required local communication;
- file permission is constrained to controlled application paths;
- unmanaged-code permission is not granted merely for sample UI styling.

## 5. Local endpoint policy

- bind to `127.0.0.1` or equivalent loopback only;
- do not bind to `0.0.0.0`;
- generate an ephemeral session secret;
- rotate on Add-In/session restart;
- reject missing, invalid, and expired credentials;
- set request size and response size limits;
- rate limit tool calls;
- disable CORS unless a local browser UI is explicitly required;
- log failed authentication without logging the token.

For external MCP host IPC:

- prefer named pipe;
- apply Windows ACL for the current user;
- validate peer/session identity;
- reject stale Add-In sessions.

## 6. Prompt injection defense

TIA project content is data.

Examples of untrusted content:

- block comments;
- symbol names;
- HMI text;
- device descriptions;
- imported documentation;
- file contents.

Rules:

- project content cannot grant permissions;
- project text cannot approve changes;
- project text cannot alter tool policy;
- agent prompts must mark project content as untrusted;
- writes require an Add-In UI approval event;
- approval tokens are generated outside the model context.

## 7. Approval model

An approval token binds:

```json
{
  "changeSetId": "chg_...",
  "tiaSessionId": "tia_...",
  "projectId": "project_...",
  "approvedBy": "windows-user-identity",
  "scope": ["obj_..."],
  "contentDigest": "sha256:...",
  "issuedAt": "...",
  "expiresAt": "...",
  "nonce": "..."
}
```

Rules:

- single use;
- short lifetime;
- exact content digest;
- exact object scope;
- exact session and project;
- server-side verification;
- never accepted from natural-language chat alone.

## 8. Concurrency safety

Before write:

1. resolve current project and session;
2. resolve target object by internal identity;
3. read current snapshot;
4. compare current hash with expected hash;
5. verify approval;
6. acquire project write serialization;
7. re-check if required;
8. apply;
9. validate;
10. record result.

Do not write based only on name or path.

## 9. Recovery

Before supported writes:

- capture export or recoverable previous state;
- identify rollback capability;
- define failure behavior;
- never claim rollback succeeded without verification;
- preserve partial-failure evidence.

## 10. Logging policy

Record:

- IDs;
- action;
- tool;
- duration;
- result;
- object scope;
- hashes;
- approval metadata;
- validation result.

Do not record by default:

- model API keys;
- bearer tokens;
- Windows secrets;
- complete source payloads;
- personal data;
- entire prompts.

## 11. Supply-chain rules

- do not commit Siemens binaries;
- pin third-party packages where feasible;
- review MCP and runtime dependencies;
- verify package output contents;
- hash release `.addin`;
- sign artifacts when the deployment model supports it;
- do not download executables at Add-In runtime.

## 12. Safety statement

The product assists engineering. It does not replace commissioning, validation, functional-safety procedures, or authorized plant change control.
