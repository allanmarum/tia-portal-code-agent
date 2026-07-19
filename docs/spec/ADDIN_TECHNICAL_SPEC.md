# TIA Portal V21 Add-In Technical Specification

**Status:** implementation baseline  
**Target:** Siemens TIA Portal V21  
**Runtime:** Microsoft .NET Framework 4.8, 64-bit Windows  
**Primary language:** C#  
**Audience:** code agents, maintainers, reviewers, and release engineers  
**Last reviewed:** 2026-07-19

## 1. Purpose

This document is the authoritative repository specification for developing, packaging, installing, debugging, and maintaining TIA Portal Add-Ins targeting **TIA Portal V21**.

It supersedes repository guidance based only on:

- `TIA Add-Ins Getting Started`, Siemens Entry ID 109779415, V1.3, 2022-06;
- `TIA Portal Add-In Development Tools`, Programming Manual, 11/2023;
- TIA Portal V16, V17, V18, V19, or V20 assembly layouts;
- the former `PublicAPI\Vxx.AddIn` directory;
- the monolithic `Siemens.Engineering.AddIn.dll` assembly.

The older documents remain useful for concepts and examples, but they are not authoritative for V21 paths, assemblies, compatibility, installation folders, or migration behavior.

## 2. Source precedence

When sources disagree, apply this order:

1. TIA Portal Openness V21 readme and V21 online documentation.
2. Templates and files installed with the local TIA Portal V21 Add-In Development Tools.
3. TIA Portal V21 product documentation.
4. The 11/2023 Add-In Development Tools PDF.
5. The 2022 Getting Started application example.
6. Repository assumptions or historical implementation behavior.

Do not preserve an old implementation solely because it worked on V20 or earlier.

## 3. Non-negotiable V21 rules

The implementation MUST comply with all rules below.

1. Target **TIA Portal V21** explicitly.
2. Target **.NET Framework 4.8**.
3. Build and run as **64-bit**. V21 does not support Add-In development or execution on 32-bit computers.
4. Rebuild every legacy Add-In for V21. A package built for V20 or older must be treated as incompatible.
5. Do not claim backward compatibility. Add-Ins targeting V21 or later do not run on earlier TIA Portal versions.
6. Do not reference the removed monolithic `Siemens.Engineering.AddIn.dll`.
7. Reference only the required modular V21 assemblies.
8. Do not use the removed `PublicAPI\V21.AddIn` path.
9. Install developer packages as **User Add-Ins** under the current user's roaming profile.
10. Do not place custom packages in the System Add-In directory.
11. Keep Openness engineering objects out of static fields, instance fields, properties, and long-lived structures.
12. Do not use `--skipEngMemberCheck` in normal builds.
13. Do not run model calls, network requests, exports, compilation, or other long operations in menu-status callbacks.
14. Do not keep long-running agent work inside the Add-In execution lifetime. Use an external process for durable work.
15. Apply least privilege in `Config.xml`.
16. Never perform project writes without explicit product authorization and a controlled confirmation flow.

## 4. Add-In and Openness relationship

TIA Portal Openness is the API surface used to automate supported TIA engineering operations.

A TIA Add-In is a TIA-hosted extension that:

- integrates into supported TIA Portal UI areas or workflows;
- receives context from TIA Portal;
- uses the Openness API;
- is packaged as a `.addin` file;
- is activated by the user in the TIA Portal Add-Ins task card.

For an agent-enabled product:

```text
TIA Add-In = contextual UI adapter and controlled gateway to TIA
External runtime = long-running orchestration, model access, and agent session
Openness application service = single implementation of TIA engineering operations
```

The Add-In must not duplicate engineering logic already implemented in a shared Openness service layer.

## 5. Supported development environments

### 5.1 Microsoft Visual Studio

The Siemens V21 Add-In Development Tools documentation supports:

- Microsoft Visual Studio 2019;
- Microsoft Visual Studio 2022;
- .NET Framework 4.8;
- TIA Portal V18 or newer installed on the development machine.

The Siemens extension is distributed as a VSIX file similar to:

```text
TIA_Portal_Add-In_DevTools_VS2019.vsix
TIA_Portal_Add-In_DevTools_VS2022.vsix
```

The Siemens documentation states that Visual Studio Community editions are not supported by the Add-In Development Tools.

**Repository default:** Visual Studio 2022 with the V21 Siemens extension.

### 5.2 Visual Studio Code

The V21 documentation also supports Visual Studio Code using:

- the Siemens VS Code VSIX extension;
- the Siemens Add-In project/code templates distributed as a NuGet package;
- the Microsoft C# extension;
- .NET Framework 4.8 Developer Pack;
- an SDK capable of running `dotnet new` templates.

The Siemens documentation still lists .NET Core 3.1, .NET 5, or .NET 6 SDK for the template/compiler tooling. This does not change the Add-In runtime target: the Add-In itself targets .NET Framework 4.8.

### 5.3 Project type

Use a class library compatible with the V21 Add-In API.

Required project settings:

```text
Target framework: .NET Framework 4.8
Platform target: x64
Language: C#
Output: class library
```

Do not retarget the Add-In project to modern `.NET` solely for convenience.

## 6. V21 PublicAPI layout

V21 merges the former Openness and Add-In assembly directories.

Expected installation root:

```text
C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\
```

The former path below is obsolete and must not be used:

```text
C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21.AddIn\
```

Build tooling must discover the installed path rather than assuming that the system drive, Program Files path, or Portal installation path is fixed.

Preferred discovery order:

1. explicit repository/build property;
2. V21 Openness registry metadata;
3. validated default installation path;
4. fail with an actionable error.

Example property:

```xml
<PropertyGroup>
  <TiaPortalVersion>V21</TiaPortalVersion>
  <TiaPublicApiDir Condition="'$(TiaPublicApiDir)' == ''">$(ProgramFiles)\Siemens\Automation\Portal V21\PublicAPI\V21\net48</TiaPublicApiDir>
</PropertyGroup>
```

The build must fail before compilation when the directory or required assemblies are absent.

## 7. Modular V21 assemblies

TIA Portal V21 replaces the previous monolithic Openness/Add-In assemblies with modular assemblies.

### 7.1 Minimum Add-In references

A basic Add-In requires at least:

```text
Siemens.Engineering.AddIn.Base.dll
Siemens.Engineering.Base.dll
```

### 7.2 STEP 7 Add-In references

An Add-In that uses STEP 7 APIs normally requires:

```text
Siemens.Engineering.AddIn.Base.dll
Siemens.Engineering.Base.dll
Siemens.Engineering.AddIn.Step7.dll
Siemens.Engineering.Step7.dll
```

### 7.3 Safety Add-In references

A Safety Add-In normally requires the relevant Safety modules and dependencies, including:

```text
Siemens.Engineering.AddIn.Base.dll
Siemens.Engineering.Base.dll
Siemens.Engineering.AddIn.Safety.dll
Siemens.Engineering.Safety.dll
Siemens.Engineering.Step7.dll
```

Add the exact dependency set required by the APIs used in the project and the installed V21 documentation.

### 7.4 Optional Add-In assemblies

Use only when required:

```text
Siemens.Engineering.AddIn.Permissions.dll
Siemens.Engineering.AddIn.Utilities.dll
```

Examples:

- `Permissions` is relevant to package permission declarations.
- `Utilities` may be needed when starting another process from the Add-In.

### 7.5 Product-specific Openness modules

Reference product modules only when the feature requires them, for example:

```text
Siemens.Engineering.WinCC.dll
Siemens.Engineering.WinCCUnified.dll
Siemens.Engineering.Startdrive.dll
Siemens.Engineering.SafetyValidation.dll
Siemens.Engineering.Testsuite.dll
```

Do not reference every installed Siemens assembly.

### 7.6 Siemens binary policy

- Do not commit Siemens assemblies to source control.
- Do not redistribute Siemens assemblies as repository artifacts.
- Do not include Siemens assemblies in generic MCP, HTTP, or domain-contract projects.
- Isolate Siemens references in the TIA integration project.
- Keep DTOs and application contracts independent from Siemens types.

### 7.7 `Copy Local` documentation conflict

The V21 Openness readme states that copying Siemens Engineering assemblies locally is unsupported and instructs developers to set `Copy Local` to `False`. A separate V21 Add-In programming page currently instructs setting `Local copy` to `True` for `Siemens.Engineering.AddIn.Base`.

Repository policy:

1. Do not package or redistribute `Siemens.Engineering*.dll` files.
2. Prefer the settings produced by the installed V21 Add-In project template.
3. For manually maintained references, follow the V21 Openness readme and use `Copy Local = False` unless a Siemens-provided V21 template demonstrably requires another setting.
4. Add a package inspection test that fails if Siemens Engineering assemblies are embedded as application-owned dependencies.
5. Document any exception with the installed TIA V21 update level and a reproducible load test.

Do not silently choose a behavior when the local Siemens template and the readme differ.

## 8. V21 compatibility and migration

### 8.1 Compatibility boundary

TIA Portal Openness V21 was redesigned around modular assemblies.

Consequences:

- Add-Ins built for V20 or older do not run on V21 without adaptation and recompilation.
- Add-Ins built for V21 or later do not run on earlier TIA Portal versions.
- A single binary must not be advertised as a universal V20/V21 Add-In.

### 8.2 Automatic conversion limitation

The V21 Add-In Development Tools do not automatically convert older Add-In projects to V21 because the old assembly was split into multiple modules.

Migration is manual.

### 8.3 Mandatory migration steps

For every V20-or-older project:

1. Create a migration branch.
2. Preserve a reproducible build of the last legacy version.
3. Retarget the project to `.NET Framework 4.8` and `x64`.
4. Remove `Siemens.Engineering.AddIn.dll`.
5. Remove old `Siemens.Engineering.dll` references where applicable.
6. Add `Siemens.Engineering.AddIn.Base.dll` and `Siemens.Engineering.Base.dll`.
7. Add only required product-specific modular assemblies.
8. Update namespaces and types changed by the assembly split.
9. Replace legacy Feedback, Progress, and MessageBox API access with the V21 service-provider pattern.
10. Remove all obsolete `Vxx.AddIn` paths.
11. Update `Config.xml` to the V21 Publisher schema.
12. Run the V21 Publisher without `--skipEngMemberCheck`.
13. Install the output as a V21 User Add-In.
14. Activate it and validate requested permissions.
15. Test every supported context-menu object and workflow.
16. Compile representative TIA projects and record results.
17. Confirm that no legacy Siemens assembly is shipped in the package.

### 8.4 V21 service-provider API updates

Use the V21 service-provider pattern for Add-In APIs.

Conceptual examples:

```csharp
var feedback = tiaPortal.GetService<FeedbackProvider>();
feedback.Log(NotificationIcon.Information, "Operation started");
```

```csharp
using (var progress = context.GetService<ProgressProvider>())
{
    progress.Update("Processing", "Reading selected engineering objects");

    if (progress.IsCancelRequested)
    {
        // Exit cleanly and return a canceled result.
    }
}
```

```csharp
var messageBox = tiaPortal.GetService<MessageBoxProvider>();
var result = messageBox.ShowConfirmation(/* V21 arguments */);
```

Use exact namespaces and method signatures from the installed V21 SDK/templates.

## 9. Add-In provider types

The provider determines where an Add-In is exposed.

Supported provider families include:

```text
ProjectTreeAddInProvider
GlobalLibraryTreeAddInProvider
ProjectLibraryTreeAddInProvider
DevicesAndNetworksAddInProvider
VciAddInProvider / VciEditorAddInProvider, depending on API context
```

The V21 development templates also include workflow-oriented Add-Ins such as:

```text
VCI Import Add-In
VCI Repository Export Add-In
CAX Export Import Add-In
```

### 9.1 Devices and Networks limitation

The Siemens documentation states that `DevicesAndNetworksAddInProvider` cannot display its shortcut menu in at least these areas:

- `I/O communication` in network view;
- the `Connections` table in network view.

Do not treat absence in those areas as a generic loader defect.

### 9.2 CAX workflow behavior

For the CAX Export Import Add-In:

- export callbacks are restricted to write-protected operations;
- import callbacks support write operations from TIA Portal V20 onward;
- the Add-In must be selected as the default Add-In for the CAx data-exchange workflow.

Keep export and import permissions separate in the application layer.

### 9.3 Provider responsibilities

Provider classes must:

- be public and loader-discoverable;
- register supported context-menu or workflow components;
- receive/use the TIA context exactly as defined by the selected API;
- contain composition/bootstrap logic only;
- avoid business logic and network calls.

Conceptual V21 shape:

```csharp
using Siemens.Engineering.AddIn.Base;
using Siemens.Engineering.AddIn.Base.Menu;

public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        yield return new ProjectTreeContextMenu("AI Assistant");
    }
}
```

Do not copy constructor signatures from pre-V21 samples without checking the V21 template.

## 10. Context-menu implementation

The context-menu adapter may define:

- submenus;
- simple actions;
- check-box actions;
- option/radio actions;
- icons and localized text.

Icons and multilingual text support are available from V20 onward and therefore apply to V21.

### 10.1 Required layering

Use this flow:

```text
TIA context-menu adapter
  -> immutable command request
  -> TIA command dispatcher
  -> application service
  -> Openness adapter
  -> DTO/result
  -> TIA feedback or external runtime
```

Do not place all logic in the context-menu class.

### 10.2 Selection handling

- Register actions only for supported selection types.
- Validate the selection count.
- Convert selected Siemens objects to local DTOs or session-scoped handles.
- Do not pass live Siemens objects to an external process.
- Do not rely on display names as stable identifiers.
- Revalidate the object before every write.

### 10.3 Status callbacks

Status evaluation must be fast, deterministic, and side-effect free.

Allowed checks:

- supported type;
- supported number of selected objects;
- active-project state;
- feature flag;
- read/write policy;
- local runtime availability from an in-memory health snapshot.

Forbidden work:

- HTTP or model calls;
- export/import;
- project compilation;
- file-system scans;
- project mutation;
- process start;
- long Openness traversal.

## 11. Add-In lifetime and long-running work

Siemens V21 documentation warns that work continuing in a background thread after Add-In execution can be canceled and recommends starting a new process for longer-running tasks.

It also documents that Add-Ins have been kept loaded for performance since V20, which makes stale engineering-object references more dangerous.

Repository interpretation:

- Treat each command execution as a bounded TIA interaction.
- Do not assume a background task inside the Add-In will survive reliably.
- Do not keep engineering objects across executions.
- Use a local external process for agent sessions, model calls, indexing, MCP transport, telemetry, and other durable work.
- The Add-In may start or connect to that process only when the package permissions allow it.
- The external process must call back through a controlled local contract and must not create a second, duplicate implementation of project logic.

Recommended architecture:

```text
TIA Portal
  -> Add-In command/UI adapter
  -> bounded TIA operation gateway
  <-> authenticated local IPC
  -> external Agent Runtime / MCP Host
  -> model provider
```

The Add-In remains the authority for operations tied to the active in-process TIA context.

## 12. Engineering-object lifetime

From V20 onward, Add-Ins may remain loaded. Siemens Publisher therefore checks for engineering objects stored as members.

Rules:

- `TiaPortal` may be retained only where the API explicitly supports it.
- All other engineering objects must remain in local method scope.
- Do not place Siemens engineering objects in:
  - static fields;
  - instance fields;
  - properties;
  - records or DTOs;
  - dependency-injection singletons;
  - caches;
  - task state captured beyond the command.
- Persist only serializable identifiers, names for display, paths for diagnostics, hashes, and snapshots.
- Re-resolve the engineering object on each operation.
- Treat an object as invalid after project close, object deletion, structural mutation, or session change.

The build must publish without `--skipEngMemberCheck`.

## 13. `Config.xml`

The package configuration defines:

- Publisher schema version;
- author and description;
- Add-In version;
- product name, ID, and version;
- feature assembly;
- optional PDB;
- optional additional assemblies;
- Multiuser display behavior;
- TIA permissions;
- security or unrestricted permissions;
- optional certificates.

### 13.1 Schema

Use the V21 Publisher namespace copied exactly from the installed V21 template/documentation.

Conceptual form:

```xml
<PackageConfiguration
  xmlns="http://www.siemens.com/Automation/Openness/AddIn/Publisher/V21">
```

Do not derive schema casing or version from an old file.

### 13.2 Minimal conceptual structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<PackageConfiguration xmlns="V21_SCHEMA_FROM_INSTALLED_TEMPLATE">
  <Author>Product team</Author>
  <Description>Contextual TIA Portal engineering assistant.</Description>
  <AddInVersion>1.0.0</AddInVersion>

  <Product>
    <Name>TIA Engineering Assistant</Name>
    <Id>stable-product-id</Id>
    <Version>1.0.0.0</Version>
  </Product>

  <FeatureAssembly>
    <AssemblyInfo>
      <Assembly>path-to-feature.dll</Assembly>
      <Pdb>path-to-feature.pdb</Pdb>
    </AssemblyInfo>
  </FeatureAssembly>

  <RequiredPermissions>
    <TIAPermissions>
      <TIA.ReadOnly />
    </TIAPermissions>
    <SecurityPermissions>
      <!-- Add only permissions mapped to implemented features. -->
    </SecurityPermissions>
  </RequiredPermissions>
</PackageConfiguration>
```

The example is structural. Generate the actual file from the installed V21 template and validate it with the V21 Publisher.

### 13.3 Product identity

- Keep `Product/Id` stable across compatible upgrades.
- Version package changes deliberately.
- Do not reuse an ID across unrelated products.
- Include the target TIA version in artifact naming, not necessarily in the stable product ID.

Example artifact:

```text
TiaEngineeringAssistant-v1.4.0-tia-v21.addin
```

### 13.4 Additional assemblies

Every additional application-owned DLL must be declared explicitly.

Do not add:

- Siemens Engineering runtime DLLs;
- unused UI frameworks;
- test assemblies;
- build-only tooling;
- secrets or environment-specific configuration.

### 13.5 PDB policy

- Include PDBs in internal debug packages when needed.
- Exclude or separately distribute PDBs in production according to release policy.
- Never allow a missing optional PDB to invalidate a production package unexpectedly.

## 14. Permissions

### 14.1 TIA permissions

Use one of:

```xml
<TIA.ReadOnly />
```

or:

```xml
<TIA.ReadWrite />
```

Do not combine them.

Default for the first agent-enabled release:

```text
TIA.ReadOnly
```

Add write permission only when write features are implemented, reviewed, auditable, and guarded by explicit user approval.

### 14.2 Security permissions

Request only permissions required by implemented behavior.

Potential examples include:

- UI permission;
- file dialog permission;
- file I/O permission;
- environment permission;
- web/network permission;
- process-start permission.

Every requested permission must have:

- a linked feature;
- a threat assessment;
- a test proving the feature fails safely without it;
- a user-facing explanation;
- reviewer approval.

### 14.3 Unrestricted permissions

`SecurityPermissions` and `UnrestrictedPermissions` are mutually exclusive.

Unrestricted access gives the Add-In the rights of the logged-in user and increases risk significantly.

Rules:

- do not use unrestricted access by default;
- use it only when required by a concrete API or dependency;
- include a justification between 10 and 120 characters;
- display the same rationale in product security documentation;
- treat every introduction of unrestricted access as a security-breaking change.

### 14.4 Agent-runtime permissions

An Add-In that starts an external runtime or connects over local HTTP/IPC may require process-start and/or network permissions.

Prefer:

- loopback-only endpoints;
- named pipes with user ACLs;
- ephemeral authentication tokens;
- no inbound LAN listener;
- no embedded model-provider secret in the `.addin` package.

## 15. Package generation

Use the V21 `Siemens.Engineering.AddIn.Publisher.exe` located under the TIA Portal installation `PublicAPI` area.

Supported Publisher arguments include:

```text
--configuration / -f
--certificatepassword / -p
--logfile / -l
--outfile / -o
--verbose / -v
--console / -c
--pause / -x
--help / -h
--skipEngMemberCheck / -s
```

### 15.1 Build command

Conceptual PowerShell:

```powershell
& $publisher `
  --configuration $configPath `
  --outfile $outputAddIn `
  --logfile $publisherLog `
  --verbose `
  --console

if ($LASTEXITCODE -ne 0) {
    throw "TIA Portal V21 Add-In publishing failed with exit code $LASTEXITCODE."
}
```

Do not use `--skipEngMemberCheck` in CI or release builds.

### 15.2 Packaging requirements

- Quote every path.
- Produce deterministic artifact names.
- Capture Publisher logs as build artifacts.
- Fail the build on Publisher warnings classified as release blockers.
- Verify that the artifact exists and is non-empty.
- Compute and publish SHA-256.
- Inspect package contents for prohibited Siemens runtime DLLs.
- Do not hard-code an old `Portal V16`, `V17`, `V18`, `V19`, or `V20` path.
- Support packaging from a standalone script, not only an IDE post-build event.

## 16. Installation folders in V21

### 16.1 User Add-Ins

Use this folder for development and user-scoped installation:

```text
C:\Users\<UserID>\AppData\Roaming\Siemens\Automation\Portal V21\UserAddIns
```

TIA Portal V21 can also copy a selected package to this directory through the `User Add-Ins` action in the Add-Ins task card.

### 16.2 Corporate Add-Ins

Corporate packages are synchronized by the Siemens Add-In Rollout Service under:

```text
C:\ProgramData\Siemens\Automation\Portal V21\CorporateAddIns
```

Corporate rollout is a separate deployment mode and must not be simulated by arbitrary file copying without understanding the rollout service.

### 16.3 System Add-Ins

System Add-Ins are installed by Siemens products.

Do not:

- add custom packages to the System Add-In folder;
- overwrite Siemens packages;
- delete System Add-Ins;
- use the TIA installation directory as the default V21 developer deployment target.

### 16.4 Installation behavior

The installer/deployment script must:

- verify the target is V21;
- detect an existing package with the same product ID;
- show the current and incoming versions;
- avoid silent overwrite unless explicitly configured for automation;
- retain or export a rollback copy;
- record package hash and installation timestamp;
- avoid administrator elevation for normal User Add-In deployment.

## 17. Activation

Expected activation flow:

1. Open TIA Portal V21.
2. Open the `Add-Ins` task card.
3. Add or locate the package under `User Add-Ins` or the relevant managed category.
4. Review package identity, certificate information, and requested permissions.
5. Activate the Add-In.
6. Invoke the command from a supported object or workflow.

The product must use clear permission descriptions. A user must be able to understand why file, network, process, or unrestricted access is requested.

## 18. Openness installation and Windows group

TIA Portal Openness is an inherent feature of TIA Portal V21 and is installed with TIA Portal. The installed modular assemblies vary by installed TIA products/options.

Windows access is controlled through the local group:

```text
Siemens TIA Openness
```

Requirements:

- the executing user must be a direct or indirect member;
- an administrator manages the group membership;
- after membership changes, the affected user must fully sign out and sign in again;
- missing group membership should be reported separately from package activation or loader errors;
- setup tooling must not silently add users to the group.

A missing membership can result in `EngineeringSecurityException` when accessing a TIA Portal process through Openness.

## 19. UI and feedback

Prefer native V21 Add-In APIs for standard messages:

- Feedback API for Inspector/log messages;
- Progress API for supported progress and cancellation;
- MessageBox API for notifications and confirmations.

Use custom WPF UI only when native mechanisms are insufficient.

### 19.1 UI rules

- Never block the TIA UI while waiting for a model or network response.
- Do not use a modal window for a long-running agent session.
- Provide cancellation.
- Keep window ownership and dispatcher usage explicit.
- Separate views, view models, and TIA services.
- Do not request unmanaged-code permission solely to reproduce sample visual chrome.
- Do not hide failures behind a generic success message.

### 19.2 External agent UI

For long-running agent tasks, prefer:

```text
Add-In command
  -> capture immutable request context
  -> start or contact local runtime
  -> show immediate acknowledgement in TIA
  -> display progress/result through a controlled UI channel
```

Do not pass live Openness objects to the external runtime.

## 20. Threading, concurrency, and cancellation

All access to TIA objects must follow the execution/threading constraints of the V21 API and the generated template.

Use a dispatcher abstraction:

```text
Add-In callbacks / IPC requests
  -> TiaCommandDispatcher
  -> session validation
  -> cancellation/timeout
  -> serialized write policy
  -> Openness application service
```

Rules:

- serialize write operations;
- do not hold locks while calling a model or external HTTP service;
- propagate cancellation;
- limit command duration;
- return structured errors;
- reject work after project/session invalidation;
- do not compile the same target concurrently;
- re-resolve objects at execution time.

## 21. Architecture boundaries

Recommended solution structure:

```text
src/
├── TiaAgent.AddIn.V21/
│   ├── Providers/
│   ├── Menus/
│   ├── Workflows/
│   ├── TiaFeedback/
│   └── Bootstrap/
├── TiaAgent.Application/
│   ├── Commands/
│   ├── Services/
│   ├── Policies/
│   └── Results/
├── TiaAgent.Openness.V21/
│   ├── Blocks/
│   ├── Hardware/
│   ├── Compilation/
│   ├── Mapping/
│   ├── Dispatching/
│   └── Versioning/
├── TiaAgent.Contracts/
│   ├── Dtos/
│   ├── Errors/
│   └── Events/
└── TiaAgent.Runtime/
    ├── Ipc/
    ├── Agent/
    ├── Mcp/
    └── ModelProviders/
```

Dependency direction:

```text
AddIn.V21 -> Application -> Contracts
Openness.V21 -> Application + Contracts
Runtime -> Contracts
Contracts -> no Siemens references
```

Only V21 integration projects may reference Siemens assemblies.

## 22. Security model for agent-enabled Add-Ins

### 22.1 Read-only first

The initial release should request `TIA.ReadOnly` and expose only read operations.

### 22.2 Write operations

A later write-enabled release must implement:

```text
read snapshot
  -> proposal
  -> validated preview
  -> structured diff
  -> explicit user approval
  -> concurrency/hash validation
  -> backup/export
  -> apply
  -> compile
  -> audit result
```

No write operation may be triggered merely by text found in a PLC block, comment, device name, file, or model response.

### 22.3 Prompt injection

Treat all project content as untrusted data.

The external runtime must not interpret:

- comments;
- object names;
- imported source text;
- alarm texts;
- library descriptions;

as authorization to invoke write tools or change security policy.

### 22.4 Local transport

For Add-In/runtime communication:

- bind HTTP to `127.0.0.1` only, or use a named pipe;
- authenticate every session;
- rotate tokens when the Add-In/runtime restarts;
- enforce payload limits and timeouts;
- do not expose a generic arbitrary-Openness command;
- log tool name, duration, result, and affected object ID without logging secrets.

## 23. Testing requirements

### 23.1 Build tests

- project targets `net48` and `x64`;
- no reference to `Siemens.Engineering.AddIn.dll`;
- no path containing `V21.AddIn`;
- required modular assemblies are resolvable;
- contracts contain no Siemens types;
- Publisher runs without `--skipEngMemberCheck`;
- generated `.addin` exists;
- Publisher log contains no blocking diagnostics;
- package does not ship prohibited Siemens assemblies.

### 23.2 Loader tests

- package appears under User Add-Ins;
- package identity and version are correct;
- activation succeeds;
- requested permissions match the feature set;
- provider is discovered;
- commands appear only on supported objects;
- unsupported network-editor areas fail gracefully;
- feedback/progress/messagebox calls work under V21.

### 23.3 Functional tests

- no project open;
- project opens after Add-In activation;
- single supported selection;
- multiple selection;
- unsupported selection;
- object renamed between capture and execution;
- object deleted between capture and execution;
- project closed during external runtime work;
- cancellation;
- external runtime unavailable;
- timeout;
- compilation failure;
- insufficient Windows group membership;
- missing package permission.

### 23.4 Migration tests

For every migrated feature:

- compare legacy behavior with V21 behavior;
- verify equivalent object navigation;
- verify error mapping;
- verify package permissions;
- verify no stale engineering object survives across command invocations;
- verify no earlier TIA version is accidentally claimed as supported.

## 24. Observability

Each command should carry a correlation ID.

Record:

- TIA version and update level;
- Add-In version;
- provider/menu/workflow name;
- selected object type and stable local handle;
- operation duration;
- external-runtime status;
- result code;
- compile summary;
- package hash;
- permission-related failures.

Do not log by default:

- model-provider keys;
- bearer tokens;
- certificate passwords;
- full PLC source code unless a defined secure policy allows it;
- sensitive project data unrelated to diagnosis.

## 25. Error taxonomy

Use structured errors, for example:

```text
TIA_V21_NOT_INSTALLED
TIA_PUBLIC_API_NOT_FOUND
TIA_REQUIRED_ASSEMBLY_NOT_FOUND
TIA_ADDIN_PACKAGE_INVALID
TIA_ADDIN_ACTIVATION_REQUIRED
TIA_OPENNESS_GROUP_REQUIRED
TIA_PROJECT_NOT_OPEN
TIA_SESSION_INVALID
TIA_OBJECT_NOT_FOUND
TIA_OBJECT_CHANGED
TIA_SELECTION_NOT_SUPPORTED
TIA_PERMISSION_DENIED
TIA_OPERATION_NOT_SUPPORTED
TIA_PUBLISHER_FAILED
TIA_COMPILE_FAILED
TIA_TIMEOUT
TIA_CANCELLED
AGENT_RUNTIME_UNAVAILABLE
AGENT_RUNTIME_AUTH_FAILED
```

Errors must identify whether the failure occurred in:

- build;
- package publishing;
- installation;
- activation;
- Windows authorization;
- TIA/Openness operation;
- external runtime;
- model provider.

## 26. Definition of done

A V21 Add-In feature is complete only when:

- it builds against V21 modular assemblies;
- it targets .NET Framework 4.8 and x64;
- it publishes without bypassing the engineering-member check;
- it installs in the V21 User Add-Ins folder;
- it activates with the minimum required permissions;
- it does not retain engineering objects outside local operation scope;
- it does not block the TIA UI;
- it handles cancellation and errors;
- it is covered by unit and V21 integration tests;
- it documents any unavailable Openness capability;
- it has no undocumented write behavior;
- it has no Siemens runtime binaries committed or redistributed;
- it has been manually validated in TIA Portal V21.

## 27. Prohibited implementation patterns

Do not:

- reference `Siemens.Engineering.AddIn.dll` in V21 code;
- reference the old `PublicAPI\V21.AddIn` directory;
- reuse a V20 `.addin` package in V21;
- claim a V21 package runs on V20 or earlier;
- target AnyCPU or x86;
- cache `IEngineeringObject` instances;
- store blocks, devices, projects, or selections as class members;
- use `--skipEngMemberCheck` to make a release pass;
- copy all sample security permissions;
- request unrestricted access without a reviewed reason;
- package Siemens Engineering DLLs as product-owned files;
- run HTTP/model calls in menu status evaluation;
- run a durable agent loop inside the Add-In callback;
- use object names as concurrency-safe identifiers;
- perform a write without preview and explicit approval;
- close a project from an Add-In as a normal workflow;
- assume all TIA UI-visible properties are available through Openness;
- expose an unrestricted `execute_arbitrary_openness_operation` endpoint.

## 28. Historical notes from the attached documents

The attached 11/2023 Development Tools manual correctly describes concepts still relevant in V21:

- Add-Ins use C# and Openness;
- project, library, devices/networks, and VCI integration points;
- `.addin` packaging;
- Visual Studio and VS Code development flows;
- build/debug integration;
- `Config.xml` metadata and permissions.

The following parts are historical and must not be implemented as V21 rules:

- assuming `Vxx.AddIn` assembly directories;
- using the monolithic `Siemens.Engineering.AddIn.dll`;
- installing user-developed Add-Ins in the Portal installation `AddIns` folder;
- assuming automatic project conversion works for V21;
- assuming a package built for an earlier version will continue running on V21.

The attached 2022 Getting Started document remains useful for WPF packaging and basic Add-In concepts, but its V16/V17 paths and compatibility claims are not evidence for V21 behavior.

## 29. Authoritative online references

1. Siemens, **TIA Portal Add-In Development Tools V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-add-in-development-tools

2. Siemens, **Introduction to programming Add-Ins — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/introduction-to-the-tia-portal/extending-tia-portal-functions-with-add-ins/programming-add-ins/introduction-to-programming-add-ins

3. Siemens, **Major changes for long-term stability in TIA Portal Openness V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/readme-tia-portal-openness/major-changes-for-long-term-stability-in-tia-portal-openness-v21

4. Siemens, **Creating a C# program — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/introduction-to-the-tia-portal/extending-tia-portal-functions-with-add-ins/programming-add-ins/creating-a-c-program

5. Siemens, **Configuration file — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/introduction-to-the-tia-portal/extending-tia-portal-functions-with-add-ins/programming-add-ins/creating-an-add-in-from-the-dll/configuration-file

6. Siemens, **Creating an Add-In from the DLL — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/introduction-to-the-tia-portal/extending-tia-portal-functions-with-add-ins/programming-add-ins/creating-an-add-in-from-the-dll/creating-an-add-in-from-the-dll

7. Siemens, **Adding User Add-Ins — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/introduction-to-the-tia-portal/extending-tia-portal-functions-with-add-ins/adding-user-add-ins

8. Siemens, **Installing TIA Portal Openness — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/installation/installing-tia-portal-openness

9. Siemens, **Adding users to the Siemens TIA Openness user group — V21**  
   https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/installation/adding-users-to-the-siemens-tia-openness-user-group

10. Siemens Industry Online Support, **Sales and delivery release of TIA Portal V21**  
    https://support.industry.siemens.com/cs/document/109989772/

## 30. Repository implementation directive

When implementing or reviewing TIA Portal Add-In code, code agents must first verify:

```text
target == TIA Portal V21
framework == .NET Framework 4.8
platform == x64
assembly model == modular V21
installation == UserAddIns or managed CorporateAddIns
migration == manual from V20 or older
engineering object lifetime == local scope
publisher skip check == disabled
permissions == least privilege
long-running work == external process
```

If any condition is unknown, stop and inspect the installed V21 templates/documentation before changing code.
