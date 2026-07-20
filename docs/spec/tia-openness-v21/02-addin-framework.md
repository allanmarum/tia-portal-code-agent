# TIA Portal V21 Add-In framework

## 1. Scope

The Add-In API is delivered mainly through:

- `Siemens.Engineering.AddIn.Base`;
- `Siemens.Engineering.AddIn.Permissions`;
- `Siemens.Engineering.AddIn.Utilities`;
- `Siemens.Engineering.AddIn.Step7`;
- `Siemens.Engineering.AddIn.Safety`.

An Add-In runs in the TIA Portal extension context. It can contribute context-menu actions and workflow extensions while using the engineering object model supplied by TIA Portal.

## 2. Provider entry points

The base assembly exposes provider classes for major UI locations:

| Provider | UI scope |
|---|---|
| `ProjectTreeAddInProvider` | Project tree |
| `DevicesAndNetworksAddInProvider` | Devices and networks editor/context |
| `ProjectLibraryTreeAddInProvider` | Project library tree |
| `GlobalLibraryTreeAddInProvider` | Global library tree |

A provider returns one or more `ContextMenuAddIn` implementations for its scope. Providers are disposable and their lifetime is controlled by the Add-In framework.

### Project rule

Provider classes SHOULD be thin composition roots. They SHOULD construct menu definitions and delegate engineering work to application services. They MUST NOT contain long-running business logic.

## 3. Context menu model

`ContextMenuAddIn` is the base extension object. It provides:

- a display name supplied through its constructor;
- `BuildContextMenuItems(ContextMenuAddInRoot)`;
- submenu access through `GetSubmenu()`.

`ContextMenuAddInRoot` exposes:

- `Items`;
- `DefaultLabelText`.

`ChildItemFactory` creates menu children, including:

- submenus;
- typed action items;
- action items with status callbacks;
- check-box action items;
- radio-button action items.

The API contains typed `ActionItem<TSelectedObject>` variants for one, two and three selected-object types. This is the preferred way to constrain an action to supported engineering objects.

## 4. Selection handling

The menu API supplies `MenuSelectionProvider` variants for one, two or three selected object types.

The selection passed to an action is contextual and short-lived. The implementation SHOULD:

1. inspect the typed selection;
2. convert selected Siemens objects into internal handles/DTOs;
3. release the callback quickly;
4. perform non-UI work asynchronously through a controlled service;
5. reacquire and revalidate the target before any mutation.

Do not retain a `MenuSelectionProvider` or its live objects as a long-term cache.

## 5. Menu status and state

Status callbacks allow TIA Portal to update whether an item is enabled and how it is displayed.

Relevant types include:

- `MenuStatus`;
- `ActionItemStyle`;
- `CheckBoxActionItemStyle` and `CheckBoxState`;
- `RadioButtonActionItemStyle` and `RadioButtonState`.

Status callbacks MUST be fast, deterministic and side-effect free. They SHOULD only inspect the current selection and cheap cached state.

Never perform model calls, file I/O, compilation or deep project scans in a status callback.

## 6. User feedback APIs

### 6.1 Progress

`ProgressProvider` displays progress in TIA Portal and exposes:

- `Update(string, string)`;
- `IsCancelRequested`;
- disposal.

Long-running actions SHOULD periodically update progress and honor cancellation.

### 6.2 Messages and confirmation

`MessageBoxProvider` provides:

- `ShowNotification(...)`;
- `ShowConfirmation(...)`.

Use notifications for completed or failed user-triggered actions. Use confirmation for actions that change project state or cross a safety boundary.

### 6.3 Feedback context

`FeedbackProvider` exposes TIA Portal feedback context. Treat it as UI integration support, not as the primary logging channel.

## 7. Workflow extension model

The base workflow architecture follows this chain:

```text
AddInProvider
  -> WorkflowAddIn
    -> WorkflowSupport
      -> WorkflowItem
        -> Execute / Rollback
```

Core workflow types include:

- `WorkflowContext`;
- `WorkflowExecutionResult`;
- `WorkflowReturnCode`.

The Step7 Add-In assembly specializes this model for CAx workflows:

- `CaxAddInProvider`;
- `CaxWorkflowAddIn`;
- `CaxExportWorkflowSupport`;
- `CaxImportWorkflowSupport`;
- pre/post import and export workflow items;
- `CaxWorkflowContext` and `CaxWorkflowArgs`.

The Safety Add-In assembly specializes it for Safety compile:

- `SafetyCompileAddInProvider`;
- `SafetyCompileWorkflowAddIn`;
- `SafetyCompileWorkflowSupport`;
- `SafetyCompileWorkflowItem`;
- `SafetyCompileContext` and arguments.

Workflow items SHOULD be idempotent where possible. A rollback implementation MUST only claim success when the external and TIA-side effects are actually reversed.

## 8. External process execution

`Siemens.Engineering.AddIn.Utilities` wraps process execution with:

- `Process`;
- `ProcessStartInfo`;
- redirected standard input/output/error;
- process lifecycle and exit events.

`Siemens.Engineering.AddIn.Permissions.ProcessStartPermission` represents the custom permission used to restrict process start.

Project rules:

- External processes MUST be explicitly declared as an Add-In capability.
- Executable paths MUST be allow-listed or resolved from trusted installation configuration.
- User-controlled strings MUST NOT be concatenated into a shell command.
- Prefer direct executable invocation with an argument list.
- Capture stdout/stderr and enforce timeout/cancellation.
- Do not start the agent runtime synchronously from the TIA UI thread.

## 9. Suggested internal architecture

```text
Provider classes
  -> Menu definitions
    -> Command handlers
      -> ITiaProjectService
      -> IAgentRuntimeClient
      -> IApprovalService
      -> IAuditLog
```

A menu click SHOULD produce a small command object:

```csharp
public sealed record ExplainSelectedBlockCommand(
    TiaSessionId SessionId,
    ProjectId ProjectId,
    ObjectHandle Block,
    SelectionSnapshot Selection);
```

The command handler may then call the agent runtime. The agent runtime uses MCP tools backed by the same `ITiaProjectService`; the Add-In does not duplicate Openness navigation logic.

## 10. Add-In implementation rules

- MUST keep provider and menu-status code lightweight.
- MUST marshal UI interactions to the required TIA context.
- MUST not block the UI while waiting for an LLM or external process.
- MUST use `ProgressProvider` for user-visible long operations.
- MUST revalidate selected objects before mutation.
- MUST require confirmation for write operations.
- MUST use the Add-In process wrapper and permission model where required.
- MUST dispose providers and process wrappers.
- SHOULD log command ID, selection snapshot, operation duration and result.
