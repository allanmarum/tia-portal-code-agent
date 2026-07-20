# TIA Portal V21 Teamcenter Gateway and version-control APIs

## 1. Teamcenter Gateway assembly

`Siemens.Engineering.TeamcenterGateway` exposes integration with Teamcenter for TIA projects and global libraries.

Key types include:

- `TeamcenterConnectionProvider`;
- `TcGatewayConnectionInfo`;
- `TcGatewaySearchAndDownloadProvider`;
- `TcGatewayWorkflowProvider`;
- `TcGatewayLockProvider`;
- `ItemDetails`, `RevisionDetails` and `ItemInfo`;
- search result types;
- Teamcenter properties and list-of-value information;
- dataset, item and cache option enums;
- `TcGatewayException` and error callbacks.

Dataset types distinguish TIA project and TIA library datasets.

## 2. Domain responsibilities

The Teamcenter surface covers:

- establishing or inspecting a connection;
- searching items/revisions;
- downloading project/library content;
- creating or updating workflow-related item metadata;
- lock handling;
- mapped/custom Teamcenter properties;
- local cache behavior.

Item and revision DTOs SHOULD keep Teamcenter identity separate from local TIA project identity.

```csharp
public sealed record TeamcenterRevisionRef(
    string ItemId,
    string RevisionId,
    string DatasetType,
    string? DatasetId);
```

## 3. Locking and concurrency

Teamcenter locks and TIA project exclusive access are separate concerns. A workflow that changes a Teamcenter-managed project may require both:

1. verify Teamcenter lock state;
2. obtain the required Teamcenter lock;
3. open/attach to the local TIA project;
4. obtain TIA exclusive access for mutation;
5. commit local change;
6. save/check in through the Teamcenter workflow;
7. release locks according to policy.

Do not treat a TIA transaction as a substitute for Teamcenter revision control.

## 4. Version-control Add-In surface

`Siemens.Engineering.AddIn.Base` also contains `Siemens.Engineering.AddIn.VersionControl` APIs and provider families for VCI workspace and import workflows.

Examples include:

- workspace-view Add-In providers;
- editor Add-In providers;
- repository composite Add-Ins;
- import Add-Ins;
- workflow support around repository/import operations.

These extension points allow UI and workflow integration with version-control operations. They are distinct from the general `ProjectTreeAddInProvider` context menu.

## 5. Agent policy

Read-only Teamcenter tools may:

- inspect connection status;
- search items and revisions;
- inspect properties and lock state;
- resolve the Teamcenter source of the current project.

Mutating tools such as check-in, revision creation, metadata changes or lock changes MUST require explicit approval and a dedicated Teamcenter permission.

## 6. Recommended tools

```text
tia_teamcenter_get_connection
tia_teamcenter_search
tia_teamcenter_get_item
tia_teamcenter_get_revision
tia_teamcenter_get_lock_state
tia_teamcenter_download_preview
```

Restricted:

```text
tia_teamcenter_acquire_lock
tia_teamcenter_release_lock
tia_teamcenter_create_revision
tia_teamcenter_update_properties
tia_teamcenter_check_in
```

## 7. Error handling

Preserve:

- Teamcenter error callback message;
- `TcGatewayException` type/message;
- item/revision identifiers;
- local cache option;
- workflow operation ID;
- TIA project state if the failure occurred after opening the project.
