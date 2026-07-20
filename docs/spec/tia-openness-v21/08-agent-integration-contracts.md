# Agent-facing integration contracts for TIA Portal V21

## 1. Architectural position

This project uses the following separation:

```text
TIA Add-In = UI integration, selection context and TIA-local capability host
MCP       = typed tool contract
Agent runtime = planning, model interaction and orchestration
```

All Openness access MUST be centralized in a TIA integration layer. Add-In commands and MCP handlers call the same services.

```text
Add-In command handlers ─┐
                         ├── TIA application services ── Siemens V21 API
MCP tool handlers ───────┘
```

## 2. Layer boundaries

### 2.1 Siemens adapter layer

Responsibilities:

- own live Siemens objects;
- attach/start TIA Portal;
- navigate the EOM;
- obtain services;
- execute operations;
- translate Siemens results/exceptions;
- dispose resources.

No object from a `Siemens.Engineering.*` namespace may leave this layer.

### 2.2 Application service layer

Responsibilities:

- stable project/domain interfaces;
- authorization and approval checks;
- concurrency validation;
- temporary-file policy;
- compile/validation orchestration;
- audit logging;
- DTO creation.

### 2.3 MCP transport layer

Responsibilities:

- validate tool input schemas;
- map tool calls to application services;
- return bounded structured responses;
- never contain Siemens navigation logic.

## 3. Core service interfaces

```csharp
public interface ITiaContextService
{
    TiaContextDto GetCurrentContext();
    IReadOnlyList<ProjectSummaryDto> ListProjects();
    SelectedObjectDto GetSelection(SelectionToken token);
}

public interface IPlcQueryService
{
    IReadOnlyList<PlcSummaryDto> ListPlcs(ProjectId projectId);
    IReadOnlyList<BlockSummaryDto> ListBlocks(PlcId plcId, BlockQuery query);
    BlockArtifactDto ExportBlock(BlockHandle block, ExportProfile profile);
    ReferenceResultDto FindReferences(ObjectHandle target, ReferenceQuery query);
}

public interface IPlcMutationService
{
    ChangePreviewDto PreviewBlockImport(BlockImportProposal proposal);
    MutationResultDto ApplyApprovedBlockImport(ApprovedChange change);
}

public interface IHmiQueryService
{
    IReadOnlyList<HmiTargetSummaryDto> ListTargets(ProjectId projectId);
    IReadOnlyList<HmiScreenSummaryDto> ListScreens(HmiTargetId targetId);
    HmiObjectDto ReadObject(HmiObjectHandle handle, PropertySelection selection);
}

public interface IEngineeringValidationService
{
    CompileResultDto Compile(ObjectHandle target);
    CompareResultDto Compare(CompareRequest request);
    ValidationResultDto Validate(ObjectHandle target);
}
```

## 4. Handles and session scope

Use opaque, session-scoped handles:

```csharp
public sealed record ObjectHandle(
    string SessionId,
    string ProjectId,
    string ObjectId,
    string Kind,
    string DisplayPath,
    string VersionToken);
```

`ObjectId` is generated and resolved by the integration layer. It is not claimed to be a globally persistent Siemens identifier.

Before a write, resolve the handle again and verify:

- same TIA session;
- same project;
- same object kind;
- same expected display metadata where relevant;
- same version/content token.

## 5. DTO rules

DTOs MUST:

- be serializable without custom Siemens types;
- use bounded collections;
- include truncation flags;
- include operation/session IDs;
- include capability status;
- include a version/content token for mutable artifacts;
- avoid absolute local paths unless needed for a user-approved artifact.

DTOs MUST NOT:

- contain raw `IEngineeringObject` references;
- expose passwords or secure strings;
- contain entire HMI/project object graphs by default;
- use exception text as the only error contract.

## 6. MCP tool design

Each tool should represent one engineering intent.

Good:

```text
tia_get_current_context
tia_list_plc_blocks
tia_export_plc_block
tia_find_references
tia_compile_target
tia_preview_block_import
tia_apply_approved_change
```

Avoid:

```text
tia_invoke_any_method
tia_set_any_attribute
tia_execute_code
tia_import_any_file
tia_download_anything
```

Generic reflection tools bypass domain validation and approval boundaries.

## 7. Read/write split

Read tools may execute automatically when authorized.

Write flow:

```text
proposal
  -> preview tool
  -> diff + impact analysis
  -> user approval
  -> short-lived approval token
  -> apply tool
  -> compile/validate
  -> result + audit
```

The apply tool MUST reject:

- expired approval;
- different user/session/project;
- changed content hash;
- broader change than the approved diff;
- missing required compile/validation capability.

## 8. Risk classes

| Class | Examples | Default |
|---|---|---|
| R0 | list/read metadata | automatic |
| R1 | export, compare, cross-reference, validate | automatic and audited |
| R2 | compile, temporary generation | automatic or confirmation by policy |
| R3 | project mutation | preview + explicit approval |
| R4 | delete, broad import, connection/runtime changes | reinforced approval |
| R5 | download, Safety mutation, protection/security changes | disabled by default |

## 9. Concurrency and stale state

For text/exportable artifacts, calculate a SHA-256 hash over a canonicalized export. For metadata-only objects, derive a version token from stable observable fields and session identity.

A mutation MUST re-export or re-read the object immediately before applying the change. If the token differs, return `TIA_CONCURRENCY_CONFLICT` with a fresh snapshot.

## 10. UI-thread and latency rules

- Add-In click callbacks should capture context and return quickly.
- LLM calls MUST run outside the TIA UI thread.
- Do not hold exclusive access while waiting for the model.
- Progress updates should be sent only during actual TIA/local operation phases.
- Cancellation must propagate from TIA progress UI to the operation token.

## 11. Logging and audit

Every tool call SHOULD log:

- operation ID;
- tool name and risk class;
- user/session/project identifiers;
- target handles;
- input hash, not sensitive raw payloads;
- approval ID for writes;
- duration;
- Siemens result state/counts;
- produced artifact hashes;
- final outcome/error code.

## 12. Definition of done for a new tool

A new tool is complete only when:

- its V21 symbols are verified in the XML/API assembly;
- it uses typed application services;
- it returns DTOs only;
- input/output schemas are bounded;
- authorization and risk class are defined;
- cancellation and disposal are handled;
- errors are translated;
- write operations have preview and approval;
- integration tests cover success, unavailable capability, cancellation and stale state;
- the tool is documented in this directory.
