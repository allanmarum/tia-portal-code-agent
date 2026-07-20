---
title: TIA Portal V21 Add-In Technical Specification
document_type: technical-specification
status: proposed
audience:
  - coding-agents
  - maintainers
  - reviewers
language: en-US
---

# TIA Portal V21 Add-In Technical Specification

> Practical implementation guide for building, packaging, deploying, and debugging TIA Portal Add-Ins targeting V21.

This document complements `ARCHITECTURE.md` (system design) and `tia-openness-v21/02-addin-framework.md` (API framework). It covers the concrete engineering details needed to ship an Add-In.

---

## 1. Scope

This specification covers:

- `.addin` package structure and format;
- `Config.xml` manifest schema;
- required Siemens assemblies and reference resolution;
- Add-In provider entry points;
- build pipeline and Publisher tool;
- debugging workflow;
- deployment and installation;
- version compatibility (V18-V21).

---

## 2. `.addin` package structure

A TIA Portal Add-In is packaged as a `.addin` file - a ZIP archive with a `.addin` extension.

### 2.1 Contents

The `.addin` archive contains:

- `Config.xml` - Add-In manifest;
- `MyAddIn.dll` - compiled Add-In assembly;
- `MyAddIn.pdb` - debug symbols (optional in release);
- other resource files as needed.

### 2.2 Package creation

The `.addin` file is created by `Siemens.Engineering.AddIn.Publisher.exe` which runs automatically as a post-build step. The Publisher reads `Config.xml`, bundles the declared assemblies and PDBs, and produces the final `.addin` archive.

### 2.3 Naming

The `.addin` filename is the deployment identity. TIA Portal loads Add-Ins by scanning the UserAddIns directory for `.addin` files.

---

## 3. `Config.xml` schema

The `Config.xml` file is the Add-In manifest. It declares metadata, assemblies, and required permissions.

### 3.1 Root structure

```xml
<?xml version="1.0" encoding="utf-8" ?>
<PackageConfiguration xmlns="http://www.siemens.com/automation/Openness/AddIn/Publisher/V21">
  <Author>Author Name</Author>
  <Description>Add-In description</Description>
  <AddInVersion>1.0.0</AddInVersion>
  <Product>
    <Name>Product Name</Name>
    <Id>00000000-0000-0000-0000-000000000000</Id>
    <Version>0.0.1.0</Version>
  </Product>
  <FeatureAssembly>
    <AssemblyInfo>
      <Assembly>MyAddIn.dll</Assembly>
      <Pdb>MyAddIn.pdb</Pdb>
    </AssemblyInfo>
  </FeatureAssembly>
  <RequiredPermissions>
    <!-- See section 4 -->
  </RequiredPermissions>
</PackageConfiguration>
```

### 3.2 Elements

| Element | Description |
|---|---|
| `PackageConfiguration` | Root element. Namespace encodes the TIA version (`Publisher/V21`). |
| `Author` | Add-In author name. |
| `Description` | Human-readable description. |
| `AddInVersion` | Author-defined version string. Not used by TIA Portal for loading logic. |
| `Product/Name` | Display name in the Add-In Taskbar. |
| `Product/Id` | GUID unique identifier for the Add-In. |
| `Product/Version` | Version displayed in the Add-In Taskbar. |
| `FeatureAssembly` | Declares the main DLL and optional PDB. |
| `RequiredPermissions` | Permission declarations (see section 4). |

---

## 4. Required permissions

The `RequiredPermissions` section declares what the Add-In is allowed to do. Three sub-sections are possible.

### 4.1 `TIAPermissions`

Controls TIA Portal Openness access level:

- `<TIA.ReadWrite />` - full read/write access;
- `<TIA.ReadOnly />` - read-only access.

**MVP policy**: Use `<TIA.ReadOnly />` unless write capability is explicitly required.

### 4.2 `SecurityPermissions`

Declares individual .NET Code Access Security (CAS) permissions. Each permission requires an explicit element. Available permissions include process start, file I/O, network, registry, and UI permissions.

**MVP policy**: Only include permissions actually needed. See `SECURITY_MODEL.md` section 4 for the least-privilege policy.

### 4.3 `UnrestrictedAccess` (V19+ only)

Grants the Add-In all permissions the current user holds. Requires a justification comment (10-120 characters).

**MVP policy**: Do not use `UnrestrictedAccess`. It conflicts with the least-privilege requirement.

---

## 5. Add-In assemblies

### 5.1 Required assemblies

The Add-In references three Siemens assemblies from the TIA Portal installation:

| Assembly | Purpose |
|---|---|
| `Siemens.Engineering.AddIn.Base.dll` | Provider base classes, context menu model, user feedback APIs |
| `Siemens.Engineering.AddIn.Utilities.dll` | Process execution wrapper |
| `Siemens.Engineering.AddIn.Permissions.dll` | Permission declarations |

### 5.2 Openness assemblies (for project access)

| Assembly | Purpose |
|---|---|
| `Siemens.Engineering.Base.dll` | Core Openness API (TiaPortal, IEngineeringObject, etc.) |
| `Siemens.Engineering.dll` | High-level engineering model |
| `Siemens.Engineering.Step7.dll` | PLC-specific types |
| `Siemens.Engineering.AddIn.Step7.dll` | PLC Add-In specialization |
| `Siemens.Engineering.AddIn.Safety.dll` | Safety Add-In specialization |

### 5.3 Reference properties

All Siemens assemblies MUST be referenced with `<Private>False</Private>` (Copy Local = False). Assemblies are resolved from the TIA Portal installation at runtime.

---

## 6. Provider entry points

### 6.1 Context-menu providers

| Provider | Base class | Target UI area |
|---|---|---|
| `ProjectTreeAddInProvider` | `ProjectTreeAddInProvider` | Project tree |
| `DevicesAndNetworksAddInProvider` | `DevicesAndNetworksAddInProvider` | Devices & Networks editor |
| `ProjectLibraryTreeAddInProvider` | `ProjectLibraryTreeAddInProvider` | Project library tree |
| `GlobalLibraryTreeAddInProvider` | `GlobalLibraryTreeAddInProvider` | Global library tree |

Each provider overrides `GetContextMenuAddIns()` to yield `ContextMenuAddIn` instances.

### 6.2 VCI providers

| Provider | Base class | Target UI area |
|---|---|---|
| `VciEditorAddInProvider` | `VciEditorAddInProvider` | VCI workspace editor |
| `VciImportAddInProvider` | `VciImportAddInProvider` | VCI import |
| `VciWorkspaceRepositoryAddInProvider` | `VciWorkspaceRepositoryAddInProvider` | VCI repository export |

### 6.3 Specialized providers

| Provider | Assembly | Purpose |
|---|---|---|
| `CaxAddInProvider` | `AddIn.Step7` | CAx import/export workflows |
| `SafetyCompileAddInProvider` | `AddIn.Safety` | Safety compile workflows |

---

## 7. Assembly reference resolution

### 7.1 TIA Portal path discovery

The SDK-style csproj template uses MSBuild registry lookup:

```
HKLM\SOFTWARE\Siemens\Automation\_InstalledSW\TIAP21\EditionMain\Path
```

### 7.2 PublicAPI paths

Assemblies are located under the TIA installation:

```text
{TiaPortalLocation}\PublicAPI\V21.AddIn\
  Siemens.Engineering.AddIn.Base.dll
  Siemens.Engineering.AddIn.Utilities.dll
  Siemens.Engineering.AddIn.Permissions.dll

{TiaPortalLocation}\PublicAPI\V21\net48\
  Siemens.Engineering.Base.dll
  Siemens.Engineering.dll
  Siemens.Engineering.Step7.dll
```

---

## 8. Build and packaging

### 8.1 csproj structure

Add-In projects use SDK-style format targeting `net48`:

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
</Project>
```

### 8.2 Publisher integration

The Publisher runs automatically after build with flags: `-f` (Config.xml path), `-v` (verbose), `-c` (create package), `-l` (log file).

### 8.3 Build output

After a successful build, the output directory contains Config.xml, DLLs, PDBs, and the `.addin` package created by Publisher.

---

## 9. Debugging

### 9.1 DebugStarter

`Siemens.Engineering.AddIn.DebugStarter.exe` launches TIA Portal with the Add-In attached for debugging.

### 9.2 VS Code configuration

The template provides `.vscode/launch.json` using the `addInDebugging.launch_debug_starter` launch type.

### 9.3 Visual Studio

VS2019/VS2022 extensions (`.vsix`) provide integrated debugging support.

---

## 10. Installation

### 10.1 User Add-Ins

Add-Ins are deployed to:

```text
%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\
```

TIA Portal scans this directory on startup and loads all valid `.addin` files.

### 10.2 Company Trusted Add-Ins

Enterprise deployments use the Company Trusted Add-In Certification Tool. See `COMPANY_TRUSTED-ADD-INS.md`.

### 10.3 Loading behavior

- TIA Portal loads Add-Ins at startup.
- The Add-In Taskbar displays product name and version from Config.xml.
- Context menus appear in configured UI locations.
- The Add-In receives a `TiaPortal` instance reference.

---

## 11. Version compatibility

| TIA Version | Status | Notes |
|---|---|---|
| V18 | Minimum supported | No UnrestrictedAccess |
| V19 | Supported | UnrestrictedAccess added |
| V20 | Supported | |
| V21 | Target | Current project target |

The `Convert-AddInProject.ps1` script upgrades projects forward (V18 -> V19 -> V20 -> V21). Only forward upgrades are supported.

---

## 12. VCI extension points

### 12.1 VCI Editor

Context-menu items within the VCI workspace editor, using `WorkspaceFile` and `WorkspaceFolder` selection types.

### 12.2 VCI Import

Workflow and context-menu items for VCI import, with pre/post import workflow items.

### 12.3 VCI Repository Export

Workflow and context-menu items for VCI repository export, with pre/post export workflow items.

---

## 13. Project rules

### 13.1 Packaging

- MUST produce a valid `.addin` archive via the Publisher.
- MUST include `Config.xml` with correct namespace version.
- MUST declare only required permissions.

### 13.2 References

- MUST reference Siemens assemblies with `Copy Local = False`.
- MUST resolve assemblies from the TIA installation path.
- MUST NOT bundle Siemens assemblies in source control.

### 13.3 Deployment

- MUST deploy to UserAddIns directory (or Company Trusted store).
- MUST NOT require administrator privileges for user-level deployment.

### 13.4 Cross-references

- Architecture: `ARCHITECTURE.md`
- API framework: `tia-openness-v21/02-addin-framework.md`
- Security: `SECURITY_MODEL.md`
- Enterprise deployment: `COMPANY_TRUSTED-ADD-INS.md`
- Build plan: `IMPLEMENTATION_PLAN.md`
