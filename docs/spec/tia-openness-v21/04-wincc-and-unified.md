# TIA Portal V21 WinCC and WinCC Unified API

## 1. Two HMI object models

TIA Portal V21 exposes two distinct HMI API families:

| Product family | Assembly | Root software type |
|---|---|---|
| WinCC classic / panel-oriented engineering | `Siemens.Engineering.WinCC` | `Siemens.Engineering.Hmi.HmiTarget` |
| WinCC Unified | `Siemens.Engineering.WinCCUnified` | `Siemens.Engineering.HmiUnified.HmiSoftware` |

The models are not interchangeable. The integration MUST branch by the runtime type returned by `SoftwareContainer.Software`.

## 2. Common discovery path

```text
ProjectBase.Devices
  -> DeviceItem
    -> GetService<SoftwareContainer>()
      -> Software
        -> HmiTarget OR HmiSoftware
```

A service MUST return an explicit HMI family discriminator:

```csharp
public enum HmiFamily
{
    Classic,
    Unified
}
```

Do not infer the family from device display names.

## 3. WinCC classic — `HmiTarget`

`Siemens.Engineering.Hmi.HmiTarget` represents the classic HMI target. It exposes:

- `Connections`;
- `Cycles`;
- `GraphicLists`;
- `TextLists`;
- `ScreenFolder`;
- `ScreenTemplateFolder`;
- popup and slide-in screen folders;
- screen overview and global elements;
- `TagFolder`;
- `VBScriptFolder`;
- target metadata such as name and author.

The classic assembly contains domain namespaces for:

- screens and screen objects;
- HMI tags;
- alarms;
- runtime scripting;
- communication connections;
- logging;
- reports;
- recipes;
- scheduling;
- globalization;
- themes and dynamics.

`HmiTarget` also supports import of screen overview/global elements through SIMATIC ML operations.

### Classic HMI guidance

- Navigate through system folders and compositions rather than constructing object paths manually.
- Export/import screen or tag artifacts through their typed APIs where available.
- Keep script handling separate from graphical-object handling.
- Treat connection and alarm modifications as higher impact than text-only changes.

## 4. WinCC Unified — `HmiSoftware`

`Siemens.Engineering.HmiUnified.HmiSoftware` exposes major Unified collections directly:

- `Screens` and `ScreenGroups`;
- `Tags`, `SystemTags`, `TagTables` and `TagTableGroups`;
- `Connections`;
- `AlarmClasses`, `AnalogAlarms` and `DiscreteAlarms`;
- `AlarmLogs`, `DataLogs` and `AuditTrails`;
- `Scripts`;
- text, system-text and graphic lists;
- OPC UA alarm types;
- plant object tags;
- runtime settings.

The Unified assembly has a large UI surface covering:

- screen base objects;
- shapes, controls and widgets;
- faceplates and interfaces;
- dynamization;
- event handlers;
- control parts and trend parts;
- runtime settings;
- plant objects and plant views.

## 5. Validation model

Unified common objects derive from or follow patterns around `HmiBase` and `IValidator`.

`HmiBase.Validate()` returns a list of `HmiValidationResult` objects containing:

- property name;
- errors;
- warnings.

A Unified mutation flow SHOULD validate the modified object before compile or save. Validation warnings and errors MUST be returned as structured data.

## 6. Import/export

Unified exposes common import result objects and export interfaces such as `IChromDataExchangeExport` for supported script/module data exchange.

Classic and Unified import/export formats differ by object family. The integration MUST NOT route both through a single untyped "import HMI" operation.

Recommended contract:

```csharp
public sealed record HmiArtifactExportRequest(
    HmiHandle Target,
    HmiFamily Family,
    HmiArtifactKind Kind,
    ObjectHandle Object,
    ExportProfile Profile);
```

## 7. Efficient object extraction

Screen models can contain very large object graphs. Agent tools SHOULD return layered summaries:

1. screen metadata;
2. top-level objects and hierarchy;
3. selected properties;
4. event/dynamization references;
5. detailed properties only for requested objects.

Do not serialize every property of every screen object by default. This is expensive and creates noisy model context.

## 8. Stable DTO model

```csharp
public sealed record HmiScreenSummary(
    string Id,
    string Name,
    HmiFamily Family,
    int ObjectCount,
    IReadOnlyList<string> ReferencedTags,
    IReadOnlyList<string> Scripts,
    string ContentHash);
```

For graphical objects, use project-generated IDs scoped to the current session plus a path-like locator for diagnostics. Revalidate both the screen hash and object identity before writes.

## 9. HMI operation risk

| Operation | Default policy |
|---|---|
| List screens/tags/connections | read-only, automatic |
| Export a screen or script | read-only, audited |
| Validate Unified object | validation, automatic |
| Change text or non-runtime metadata | approved mutation |
| Change tag binding/dynamization | approved mutation with impact review |
| Change connection or runtime settings | high-impact approval |
| Delete screens/tags/alarms | high-impact approval |
| Download to HMI | separate deployment permission |

## 10. Recommended tools

```text
tia_list_hmi_targets
tia_get_hmi_overview
tia_list_hmi_screens
tia_get_hmi_screen_summary
tia_get_hmi_screen_object
tia_list_hmi_tags
tia_list_hmi_alarms
tia_list_hmi_connections
tia_export_hmi_artifact
tia_validate_unified_object
```

Mutation tools MUST be family-specific or must require an explicit `family` discriminator validated against the live target.
