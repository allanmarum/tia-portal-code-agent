# Siemens TIA Portal Openness API V21 — specification index

**Status:** project specification  
**Target:** TIA Portal V21  
**Audience:** developers, code agents, reviewers and maintainers  
**Source of truth for this document set:** Siemens XML API documentation supplied with the V21 engineering assemblies

## Purpose

This directory explains how the Siemens TIA Portal V21 engineering API is structured and how this project should consume it.

The documentation intentionally separates two related but different surfaces:

- **TIA Portal Openness:** external or attached automation of TIA Portal through `Siemens.Engineering.*` assemblies;
- **TIA Portal Add-In API:** extensions loaded into TIA Portal, mainly through `Siemens.Engineering.AddIn.*` assemblies.

Both surfaces expose the same engineering domain, but they have different entry points, lifecycles and security constraints. The supplied XML snapshot contains **30,479 documented members across 13 assemblies**.

## Document map

| File | Purpose |
|---|---|
| [`01-core-object-model.md`](./01-core-object-model.md) | TIA process, projects, Engineering Object Model, services, transactions and lifecycle |
| [`02-addin-framework.md`](./02-addin-framework.md) | Add-In providers, context menus, typed selection, progress, dialogs and workflow extensions |
| [`03-step7-plc-and-hardware.md`](./03-step7-plc-and-hardware.md) | Hardware navigation, `PlcSoftware`, blocks, tags, types, external sources and PLC services |
| [`04-wincc-and-unified.md`](./04-wincc-and-unified.md) | WinCC classic and WinCC Unified object models |
| [`05-safety-and-validation.md`](./05-safety-and-validation.md) | Safety engineering, signatures, settings, compile hooks and Safety Validation |
| [`06-engineering-operations.md`](./06-engineering-operations.md) | Compile, compare, cross-reference, import/export, CAx and download operations |
| [`07-teamcenter-and-version-control.md`](./07-teamcenter-and-version-control.md) | Teamcenter Gateway and VCI/Add-In version-control extension points |
| [`08-agent-integration-contracts.md`](./08-agent-integration-contracts.md) | Project-specific service boundaries, DTOs, MCP tool contracts and safety rules |
| [`09-api-surface-catalog.md`](./09-api-surface-catalog.md) | Generated assembly and namespace inventory |
| [`10-source-manifest.md`](./10-source-manifest.md) | Input files, checksums and extraction metadata |

## Conceptual model

```text
TIA Portal process
└── TiaPortal
    ├── Projects
    │   └── Project / ProjectBase
    │       ├── Devices
    │       │   └── DeviceItems
    │       │       └── SoftwareContainer
    │       │           ├── PlcSoftware
    │       │           ├── HmiTarget       (WinCC classic)
    │       │           └── HmiSoftware     (WinCC Unified)
    │       ├── DeviceGroups
    │       ├── Subnets
    │       └── ProjectLibrary
    ├── GlobalLibraries
    ├── HardwareCatalog
    └── Engineering services
```

The API is not a REST API. It is a local .NET object model whose objects represent live TIA Portal engineering entities. A caller navigates compositions, obtains optional services, invokes operations and disposes session-scoped resources.

## Reading order for an agent

1. Read this index.
2. Read `01-core-object-model.md` before implementing any API access.
3. Read the domain-specific file for the requested change.
4. Read `06-engineering-operations.md` before implementing compile, import, export or download.
5. Read `08-agent-integration-contracts.md` before exposing any operation as an MCP tool.

## Version and compatibility rule

The XML files describe the API surface delivered with the supplied V21 installation. The installed V21 assemblies remain the runtime authority. The implementation MUST NOT assume that an API available in another TIA Portal release exists or behaves identically in V21.

When adding a new API call:

1. verify the symbol in the V21 XML catalog;
2. verify the referenced V21 assembly;
3. isolate the call behind a project service interface;
4. add an integration test against a real or controlled V21 environment;
5. document any product or device-specific limitation.

## Project rules

- MUST keep Siemens objects inside the TIA integration boundary.
- MUST convert Siemens objects to serializable DTOs before returning data to an agent runtime.
- MUST dispose `TiaPortal`, attached sessions, exclusive-access scopes, transactions and disposable providers.
- MUST use read-only operations by default.
- MUST require explicit approval for project mutations.
- MUST treat download, Safety changes and destructive operations as high-risk.
- MUST NOT cache live `IEngineeringObject` instances across project/session changes.
- MUST NOT identify mutable objects only by display name.
