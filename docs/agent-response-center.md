# Agent Response Center

## Overview

The Agent Response Center is a standalone WPF application that displays AI agent task results in a rich, non-blocking window. It replaces the previous `MessageBox`-based result display with a professional UI that supports progress tracking, structured error presentation, Markdown rendering, and future approval workflows.

## Architecture

```
┌─────────────────┐     Process.Start()      ┌──────────────────────────┐
│  TIA Portal     │ ────────────────────────→ │  TiaAgent.ResponseCenter │
│  (AddIn net48)  │   CLI args: taskId,       │  (WPF net8.0)            │
│                 │   bridgeUrl, token,        │                          │
│                 │   action, object info      │  Polls Bridge HTTP API   │
└─────────────────┘                           │  Renders Markdown        │
                                              │  Full state machine      │
         ┌────────────────────┐               │  Cancel/Retry/Copy       │
         │  TiaAgent.Bridge   │ ←─────────────│                          │
         │  (existing)        │   HTTP API     └──────────────────────────┘
         └────────────────────┘
```

### Why Out-of-Process?

TIA Portal runs Add-Ins in a partial-trust sandbox that prevents creating WPF `Window` instances (`SecurityPermission(UnmanagedCode)` is unavailable). The ResponseCenter runs as a separate process with full WPF capabilities, launched by the AddIn via `Process.Start`.

### Communication

- **AddIn → ResponseCenter**: CLI arguments (`--task-id`, `--bridge-url`, `--token`, `--action`, etc.)
- **ResponseCenter → Bridge**: HTTP API (same endpoints the AddIn uses)
- **No new endpoints**: The Bridge is unchanged

## State Model

The ResponseCenter implements an explicit state machine:

```
Created → Submitting → Queued → Running → Completed
                                  │
                                  ├→ Failed (retryable)
                                  ├→ Cancelled
                                  ├→ WaitingForApproval (future)
                                  └→ Disconnected (network issues)
```

| State | Visual | Actions |
|-------|--------|---------|
| Created | Initial context display | — |
| Submitting | Progress bar, "Sending task…" | — |
| Queued | Progress bar, "Waiting for runtime…" | Cancel |
| Running | Progress bar, stage-specific message | Cancel |
| WaitingForApproval | Approval preview panel | Approve, Reject (future) |
| Completed | Rendered Markdown response | Copy, Close |
| Failed | Error message + details | Retry (if retryable), Close |
| Cancelled | Cancellation confirmation | Close |
| Disconnected | Connection lost message | Retry, Close |

## Markdown Rendering

The ResponseCenter renders Markdown using [Markdig](https://github.com/xoofx/markdig) and displays it in a WPF `FlowDocument`. Supported features:

- Headings (H1–H3)
- Paragraphs with bold/italic
- Bullet and numbered lists
- Inline code
- Fenced code blocks (with language labels)
- Tables
- Blockquotes
- Horizontal rules

**Fallback**: If Markdown rendering fails, the response is displayed as plain selectable text in a monospace font.

## Error Handling

Errors are presented structurally:

- **User-friendly message**: Derived from error codes or Bridge response
- **Technical details**: Expandable section with sanitized exception info
- **Correlation ID**: Displayed for support tracing
- **Retry indicator**: Shows whether retry is reasonable

### Sanitization

The following sensitive information is stripped from error display:
- Bearer tokens
- API keys in URLs
- Passwords and secrets
- Environment variable values containing sensitive keywords

## Cancellation

- User clicks **Cancel** → Bridge `POST /v1/tasks/{id}/cancel` is called
- Monitor stops polling immediately
- UI transitions to Cancelled state
- Window close also triggers cancellation if task is in progress

## Window Lifecycle

- One window per task (single-instance via named mutex)
- Duplicate launch for same task → second instance exits silently
- Window close → stops monitoring, cancels task if still running
- TIA Portal exit → ResponseCenter continues independently (safe — it's a separate process)

## CLI Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `--task-id` | Yes | Bridge-assigned task ID |
| `--bridge-url` | Yes | Bridge base URL (e.g., `http://127.0.0.1:43119`) |
| `--action` | Yes | Action name (`explain`, `review`, `propose`) |
| `--token` | No | Bearer token for Bridge auth |
| `--object-name` | No | Selected TIA object name |
| `--object-type` | No | Selected TIA object type |
| `--plc-name` | No | PLC name |
| `--project-name` | No | Project name |
| `--correlation-id` | No | Correlation ID for tracing |
| `--initial-status` | No | Initial Bridge status |
| `--initial-stage` | No | Initial Bridge stage |

## Files

### New Files

```
src/TiaAgent.ResponseCenter/
├── TiaAgent.ResponseCenter.csproj
├── Program.cs
├── Strings.cs
├── Models/
│   ├── AgentTaskState.cs
│   └── AgentResponseContext.cs
├── Services/
│   └── BridgeTaskMonitor.cs
├── ViewModels/
│   ├── AgentResponseViewModel.cs
│   ├── ErrorDetailsViewModel.cs
│   └── ApprovalPreviewViewModel.cs
├── Views/
│   ├── AgentResponseWindow.xaml
│   ├── AgentResponseWindow.xaml.cs
│   └── MarkdownRenderer.cs
└── Converters/
    └── StateConverters.cs

tests/TiaAgent.ResponseCenter.Tests/
├── TiaAgent.ResponseCenter.Tests.csproj
├── AgentResponseContextTests.cs
├── AgentResponseViewModelTests.cs
├── BridgeTaskMonitorTests.cs
├── ErrorSanitizationTests.cs
└── MarkdownRendererTests.cs

docs/
└── agent-response-center.md
```

### Modified Files

- `src/TiaAgent.AddIn/Ui/AssistantPanelFactory.cs` — Launches ResponseCenter instead of MessageBox
- `src/TiaAgent.AddIn/Providers/ProjectTreeProvider.cs` — Submits task and launches ResponseCenter (no polling)
- `TiaAgent.sln` — Added new projects
- `Directory.Packages.props` — Added Markdig package
- `tests/TiaAgent.ArchitectureTests/DependencyTests.cs` — Added ResponseCenter dependency checks

## Dependencies

- **Markdig** (0.37.0) — Markdown parsing (MIT license)
- **System.Text.Json** — JSON deserialization (built into net8.0)

## Future Work

### Approval Workflow

The `WaitingForApproval` state and `ApprovalPreviewViewModel` are placeholders for a future feature where the AI agent proposes changes that require human review before being applied. The UI will show:

- Change summary
- Affected objects
- Structured diff
- Risks and warnings
- Validation results
- Compile impact
- Approve/Reject buttons

### Process Discovery

Currently, the AddIn looks for `TiaAgent.ResponseCenter.exe` relative to its own assembly path. A future improvement could use a registry key or configuration file for the install location.
