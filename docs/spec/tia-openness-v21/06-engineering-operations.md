# TIA Portal V21 engineering operations

## 1. Purpose

This file defines how the project should implement operations that go beyond simple object navigation:

- compile;
- import/export;
- document exchange;
- compare;
- cross-reference;
- CAx transfer;
- download.

## 2. Compilation

`Siemens.Engineering.Compiler.ICompilable` defines `Compile()`.

`CompileProvider.Compile()` returns `CompilerResult`. The result exposes:

- `State`;
- `ErrorCount`;
- `WarningCount`;
- hierarchical `Messages`.

Compiler messages contain description, path, timestamp, state and nested messages/counts.

### Compile contract

A compile tool result MUST include:

```json
{
  "state": "...",
  "errorCount": 0,
  "warningCount": 0,
  "messages": [],
  "target": {},
  "durationMs": 0
}
```

The service MUST recursively flatten or preserve the hierarchy of compiler messages. It MUST NOT only return the top-level count.

## 3. SIMATIC ML import/export

Common options:

- `ExportOptions.None`;
- `ExportOptions.WithDefaults`;
- `ExportOptions.WithReadOnly`;
- `ImportOptions.None`;
- `ImportOptions.Override`;
- culture-related import options.

Typed domain objects expose their own `Export` or composition-level `Import` methods. For example, PLC blocks export from `PlcBlock` and import through `PlcBlockComposition`.

### File handling rules

- Use a per-operation temporary directory.
- Normalize and validate all paths.
- Never allow an agent to provide an unrestricted destination path.
- Calculate SHA-256 for exported/imported artifacts.
- Retain artifacts only according to configured audit policy.
- Delete temporary files on cancellation or failure where possible.

## 4. Document import/export

Step7 supports document-oriented import/export in addition to SIMATIC ML. Result types include document result state, messages and specialized results for blocks and types.

Use document exchange when the target API explicitly supports it and the workflow benefits from source-like files. Do not assume document export is available for every PLC language or object.

## 5. Compare

The base compare API includes:

- `CompareResult`;
- `CompareResultElement` tree;
- `CompareResultState` values for identical, different, missing and folder-specific states;
- software compare targets.

A compare tool SHOULD return a normalized tree:

```csharp
public sealed record CompareNode(
    string Path,
    string? LeftName,
    string? RightName,
    string State,
    string? Detail,
    IReadOnlyList<CompareNode> Children);
```

Compare results should be used to support review, not to automatically choose which side overwrites the other.

## 6. Cross-reference

`Siemens.Engineering.CrossReference` provides:

- `CrossReferenceService`;
- source and reference objects;
- reference locations;
- access and reference types;
- filters and result objects.

Cross-reference queries can be expensive. Tools SHOULD accept scope, reference type and result limits. Return truncation and continuation information explicitly.

## 7. CAx transfer

`Siemens.Engineering.Cax.CaxProvider` supports:

- export at device or project level;
- import from a CAx file;
- explicit log file variants;
- merge options through `CaxImportOptions`.

Import options include retaining existing TIA devices, overwriting and moving items to a parking-lot folder.

`TransferResult` provides state, warnings, errors and messages.

CAx import is a broad project mutation. It MUST always require preview, explicit merge policy and approval.

## 8. Download

`Siemens.Engineering.Download.DownloadProvider` supplies device download functionality. V21 exposes a large set of download configuration objects for decisions such as:

- stopping or starting modules;
- overwriting target/system data;
- initialization and reinitialization;
- protection/password handling;
- differing online/offline configurations;
- HMI component handling;
- PLC web application handling;
- user-management data;
- Safety program selection.

A download commonly requires a configuration callback/delegate to choose among options presented by the API.

### Download policy

Download is a deployment action, not a normal engineering write.

- MUST be disabled by default for autonomous use.
- MUST require a dedicated user permission.
- MUST identify the exact target interface/device.
- MUST show all configuration decisions before execution.
- MUST never guess passwords or protection choices.
- MUST return the complete `DownloadResult` message hierarchy.
- MUST not automatically start modules unless the approved plan says so.
- MUST not combine project mutation, save and download into one opaque tool.

## 9. Operation state machine

```text
requested
  -> resolved target
  -> capability checked
  -> preview generated
  -> approval required (when mutating/high risk)
  -> exclusive access acquired
  -> operation executed
  -> validation/compile
  -> committed
  -> optional save
  -> completed
```

Every state transition SHOULD be logged with an operation ID.

## 10. Standard error codes

```text
TIA_SESSION_NOT_FOUND
TIA_PROJECT_NOT_OPEN
TIA_OBJECT_NOT_FOUND
TIA_CAPABILITY_UNAVAILABLE
TIA_UNSUPPORTED_IN_V21
TIA_PERMISSION_DENIED
TIA_TRUST_REQUIRED
TIA_CONCURRENCY_CONFLICT
TIA_USER_CANCELLED
TIA_IMPORT_FAILED
TIA_EXPORT_FAILED
TIA_COMPILE_FAILED
TIA_COMPARE_FAILED
TIA_CAX_FAILED
TIA_DOWNLOAD_CONFIGURATION_REQUIRED
TIA_DOWNLOAD_FAILED
TIA_SAFETY_RESTRICTION
```

Include original exception type and technical details in diagnostics, not in the stable public error code.
