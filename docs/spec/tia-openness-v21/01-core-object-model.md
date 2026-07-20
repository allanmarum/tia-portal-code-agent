# TIA Portal V21 core object model

## 1. Scope

This specification describes the common object model implemented primarily by `Siemens.Engineering.Base`.

The core API provides:

- TIA Portal process startup and attachment;
- project creation, opening, retrieval, save, archive and close;
- navigation through devices, groups, subnets and libraries;
- generic engineering-object discovery;
- service discovery;
- exclusive access and transactions;
- common compile, compare, download and cross-reference infrastructure.

## 2. Session entry points

### 2.1 Start a TIA Portal instance

`Siemens.Engineering.TiaPortal` represents a TIA Portal session. Its constructor accepts `TiaPortalMode`.

Conceptual example:

```csharp
using Siemens.Engineering;

using var tia = new TiaPortal(TiaPortalMode.WithUserInterface);
```

Use the mode required by the product scenario. Keep the `TiaPortal` instance alive for the full operation scope and dispose it deterministically.

### 2.2 Discover and attach to a running instance

The static process APIs are:

- `TiaPortal.GetProcesses()`;
- `TiaPortal.GetProcess(...)`;
- `TiaPortalProcess.Attach()`.

`TiaPortalProcess` exposes process identity and context, including:

- `Id`;
- `Path`;
- `Mode`;
- `ProjectPath`;
- `InstalledSoftware`;
- `AttachedSessions`;
- `Attaching` event.

A process selection algorithm SHOULD use more than process ID. Prefer a match based on process ID plus project path or an explicit session selected by the user.

### 2.3 Trust and access

`TiaPortalSession` exposes:

- `AccessLevel`;
- `TrustAuthority`;
- `ProcessPath` and `ProcessId`;
- session version and attach time;
- active/utilization state.

`TiaPortalProcess.Attaching` allows TIA Portal to approve an attaching process. The project MUST treat certificate trust and access level as part of the session contract, not as an implementation detail.

## 3. Project lifecycle

`TiaPortal.Projects` is a `ProjectComposition`.

Important operations include:

- `Open(FileInfo)`;
- `Open(..., UmacDelegate)`;
- `Open(..., ProjectOpenMode)`;
- `OpenWithUpgrade(...)`;
- `Retrieve(...)` and `RetrieveWithUpgrade(...)`;
- `Create(DirectoryInfo, string)`.

`Project` provides:

- `Save()`;
- `SaveAs(DirectoryInfo)`;
- `Archive(DirectoryInfo, string, ProjectArchivationMode)`;
- `Close()`.

`ProjectBase` exposes the engineering roots:

- `Devices`;
- `DeviceGroups`;
- `UngroupedDevicesGroup`;
- `Subnets`;
- `ProjectLibrary`;
- `LanguageSettings`;
- `HistoryEntries`;
- `UsedProducts`;
- project metadata such as `Name`, `Path`, `Version`, `IsModified` and timestamps.

### Required lifecycle behavior

- A service MUST verify that the expected project is still open before every mutating operation.
- A project close or TIA disposal MUST invalidate all cached handles.
- `OpenWithUpgrade` and `RetrieveWithUpgrade` MUST never be invoked implicitly by an agent. Upgrading a project requires explicit user intent.
- `Save()` MUST be a separate approved operation; do not silently save after every mutation.

## 4. Engineering Object Model (EOM)

Most domain objects participate in the Engineering Object Model.

### 4.1 `IEngineeringObject`

The generic interface supports:

- `GetComposition(string)`;
- `GetCompositionInfos()`;
- `GetAttribute(string)` and `GetAttributes(...)`;
- `GetAttributeInfos()`;
- `SetAttribute(...)` and `SetAttributes(...)`;
- `GetInvocationInfos()`;
- `Invoke(...)`;
- `GetCreationInfos(...)`;
- `Create(...)`.

This dynamic layer is useful for inspection and generic tooling, but typed APIs SHOULD be preferred when available.

Use dynamic EOM access for:

- capability discovery;
- diagnostics;
- generic metadata explorers;
- forward-compatible inspection where a typed wrapper is intentionally unavailable.

Do not use string-based `Invoke` or `Create` as the default implementation strategy. They reduce compile-time safety and make version drift harder to detect.

### 4.2 Compositions

A composition represents an owned collection in the engineering tree. Typical composition behavior includes:

- enumeration;
- `Count`, indexed access and `Any()`;
- `Find(...)` where supported;
- type-specific `Create`, `Import` or `CreateFrom` methods.

A composition is not merely a list. It is the API boundary through which child objects are normally created, imported or located.

### 4.3 Parent relationships

Most objects expose `Parent`. Parent traversal is useful for diagnostics, but code SHOULD navigate from stable roots to children for normal operations. Reverse traversal can become ambiguous when associations or derived views are involved.

## 5. Engineering services

Objects that implement `IEngineeringServiceProvider` expose:

```csharp
T? GetService<T>();
IEnumerable<EngineeringServiceInfo> GetServiceInfos();
```

Services add optional capabilities without forcing every object to implement every operation.

Common examples include:

- `SoftwareContainer` on a hardware `DeviceItem`;
- compile services;
- download providers;
- cross-reference services;
- protection providers;
- fingerprint/checksum providers.

Recommended capability test:

```csharp
var service = objectWithServices.GetService<SomeService>();
if (service is null)
{
    return CapabilityUnavailable(...);
}
```

The integration MUST handle `null` as a normal capability result. Product edition, device type, configuration and project state can affect service availability.

## 6. Hardware-to-software navigation

The standard navigation path is:

```text
ProjectBase.Devices
  -> Device
    -> DeviceItems
      -> DeviceItem.GetService<SoftwareContainer>()
        -> SoftwareContainer.Software
```

`SoftwareContainer.Software` can then be interpreted as the relevant product model, for example:

- `Siemens.Engineering.SW.PlcSoftware`;
- `Siemens.Engineering.Hmi.HmiTarget`;
- `Siemens.Engineering.HmiUnified.HmiSoftware`.

Do not assume that every `DeviceItem` contains software. Hardware trees include racks, interfaces, modules and other items.

## 7. Exclusive access and transactions

`TiaPortal.ExclusiveAccess(...)` acquires an exclusive-access scope. `ExclusiveAccess` exposes:

- cancellation state;
- display text;
- `Transaction(ITransactionSupport, string)`.

`Transaction` represents a unit of work and exposes:

- `CanCommit`;
- `CommitRequested`;
- `CommitOnDispose()`.

Recommended mutation flow:

```text
validate session
  -> acquire exclusive access
    -> begin transaction on a supported root
      -> re-read target state
      -> verify concurrency token/hash
      -> apply bounded mutation
      -> validate result
      -> request commit on dispose
```

Rules:

- MUST keep the exclusive scope as short as practical.
- MUST surface user cancellation.
- MUST NOT perform remote model calls while holding exclusive access.
- MUST prepare and validate the proposed change before entering the transaction.
- MUST abort rather than commit if the current object no longer matches the expected version/hash.

## 8. Resource and error handling

- Dispose all disposable Siemens objects in `finally`/`using` scopes.
- Translate Siemens exceptions at the integration boundary into project error codes while preserving the original type and message in diagnostics.
- Keep user-facing errors concise; retain detailed technical context in logs.
- Treat `EngineeringNotSupportedException` as a capability/version mismatch, not as an unknown crash.
- Avoid long-running synchronous work on the TIA Portal UI thread.

## 9. Recommended project adapters

```csharp
public interface ITiaSessionGateway
{
    TiaSessionSnapshot GetSnapshot();
    ProjectHandle RequireProject(ProjectSelector selector);
}

public interface IEngineeringObjectReader
{
    EngineeringObjectSnapshot Read(ObjectHandle handle);
    IReadOnlyList<CompositionSnapshot> ListCompositions(ObjectHandle handle);
    IReadOnlyList<AttributeSnapshot> ListAttributes(ObjectHandle handle);
    IReadOnlyList<ServiceSnapshot> ListServices(ObjectHandle handle);
}

public interface IEngineeringTransactionRunner
{
    TResult Execute<TResult>(MutationPlan<TResult> plan);
}
```

These interfaces MUST return project-owned DTOs and handles, never raw Siemens objects.
