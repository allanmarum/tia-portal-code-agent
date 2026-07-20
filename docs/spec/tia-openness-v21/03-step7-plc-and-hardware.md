# TIA Portal V21 Step7, PLC and hardware API

## 1. Assemblies and namespaces

The PLC domain is primarily exposed by:

- `Siemens.Engineering.Base` for projects, hardware, devices, services, compare and download infrastructure;
- `Siemens.Engineering.Step7` for PLC software, blocks, tags, types, sources, OPC UA, alarms, technology objects and PLC-specific download options.

Important namespace families:

- `Siemens.Engineering.HW`;
- `Siemens.Engineering.HW.Features`;
- `Siemens.Engineering.SW`;
- `Siemens.Engineering.SW.Blocks`;
- `Siemens.Engineering.SW.Tags`;
- `Siemens.Engineering.SW.Types`;
- `Siemens.Engineering.SW.ExternalSources`;
- `Siemens.Engineering.SW.WatchAndForceTables`;
- `Siemens.Engineering.SW.TechnologicalObjects`;
- `Siemens.Engineering.SW.OpcUa`.

## 2. Navigate from project to PLC software

```text
ProjectBase.Devices
  -> Device.DeviceItems
    -> DeviceItem.GetService<SoftwareContainer>()
      -> SoftwareContainer.Software as PlcSoftware
```

`Device` is a container for `DeviceItem` objects. `DeviceItem` exposes module-oriented information such as:

- addresses;
- channels;
- classification;
- container/plugging state;
- position number;
- `ChangeType(...)` and `Delete()`.

Do not assume the first `DeviceItem` is the CPU. Locate a PLC software container by capability and then validate the returned software type.

## 3. `PlcSoftware` root

`Siemens.Engineering.SW.PlcSoftware` exposes the main PLC software groups:

- `BlockGroup`;
- `ExternalSourceGroup`;
- `TagTableGroup`;
- `TypeGroup`;
- `WatchAndForceTableGroup`;
- `TechnologicalObjectGroup`;
- PLC alarm text-list group.

It also provides:

- `CompareTo(...)`;
- `CompareToOnline()`;
- `UpdateProgram()`;
- service discovery through `GetService<T>()`.

The integration SHOULD treat `PlcSoftware` as the PLC aggregate root. Agent tools should be expressed relative to a PLC identifier, not relative to an arbitrary object path.

## 4. Block hierarchy

```text
PlcSoftware.BlockGroup
  -> PlcBlockGroup
    ├── Blocks: PlcBlockComposition
    └── Groups: PlcBlockUserGroupComposition
```

`PlcBlockComposition` supports:

- enumeration and `Find(...)`;
- SIMATIC ML `Import(...)`;
- document import through `ImportFromDocuments(...)`;
- creation from master copies or library type versions;
- block-specific creation methods such as `CreateFB(...)` and `CreateInstanceDB(...)`.

`PlcBlock` exposes metadata including:

- name, namespace, number and auto-numbering;
- programming language;
- consistency state;
- creation, modification, compile and download dates;
- memory layout and memory lengths;
- know-how protection state;
- multilingual title/comment;
- header author/family/name/version.

Operations include:

- `Export(FileInfo, ExportOptions)`;
- `ExportAsDocuments(...)`;
- `ShowInEditor()`;
- `Delete()`.

Concrete block types include `OB`, `FB`, `FC`, `GlobalDB`, `InstanceDB` and `ArrayDB`.

### Block read strategy

For an agent-facing read tool:

1. resolve the PLC;
2. recursively enumerate block groups;
3. find the block by a stable session handle plus current name/number metadata;
4. export to a controlled temporary location when source/body inspection requires SIMATIC ML or document export;
5. parse the exported representation outside the live Siemens object graph;
6. calculate a content hash for concurrency checks.

### Block write strategy

Prefer import-based replacement or a documented typed mutation over generic reflection. Every block write MUST:

- create a preview/diff first;
- verify the expected content hash;
- acquire exclusive access and a transaction where supported;
- import with explicit `ImportOptions`;
- compile;
- reject or roll back on compile errors;
- save only after explicit approval.

## 5. Tags and constants

`PlcSoftware.TagTableGroup` leads to tag-table groups and tables.

Relevant types include:

- `PlcTagTableGroup`;
- `PlcTagTable`;
- `PlcTag`;
- `PlcConstant`;
- `PlcSystemConstant`;
- `PlcUserConstant`.

Agent tools SHOULD distinguish:

- symbolic tag definitions;
- user constants;
- system constants;
- addresses and data types;
- table/group ownership.

Do not infer PLC program semantics from tag names alone. Use references, block exports and type information.

## 6. PLC types

`PlcSoftware.TypeGroup` exposes PLC types and nested type groups. The type domain includes `PlcTypeGroup`, its `Types`, `Groups` and associated documents.

When an agent changes a UDT or other PLC type, it MUST consider downstream blocks and data blocks. Type changes are high-impact because they can alter memory layout and require reinitialization during download.

## 7. External sources and documents

`PlcSoftware.ExternalSourceGroup` contains external sources and nested groups.

The Step7 API also defines:

- `PlcDocument` and document compositions;
- document import/export result types;
- `ImportDocumentOptions`;
- `SWImportOptions`;
- external-source operations.

Use external sources for source-oriented workflows where supported. Keep the source file, generated blocks and compile results linked by an operation ID in the audit log.

## 8. Compile and consistency

PLC objects can expose `ICompilable` or a compile service. Compilation returns `CompilerResult`, which contains:

- final state;
- error count;
- warning count;
- hierarchical messages.

`PlcBlock.IsConsistent` is a useful signal but does not replace a compile after mutation.

A successful tool result MUST include compile state and counts. Never report a write as successful merely because import returned without throwing.

## 9. Compare and cross-reference

`PlcSoftware.CompareTo(...)` and `CompareToOnline()` produce compare results. Cross-reference services are exposed through `Siemens.Engineering.CrossReference`.

Use compare for:

- before/after validation;
- offline/online difference checks;
- audit evidence.

Use cross-reference for:

- callers and callees;
- reads/writes of tags or blocks;
- impact analysis before rename/delete/type changes.

The availability and granularity of references can vary by object and project state. Tools MUST return an explicit capability/status field.

## 10. Other Step7 domains

The V21 surface also includes:

- watch and force tables;
- PLC alarms and supervision;
- technology objects, including motion-related types;
- OPC UA configuration and access control;
- units and named-value documents;
- simulation and virtual PLC settings;
- PLC-specific download configurations.

These domains should receive dedicated tools only when required. Do not expose the entire Step7 surface as one generic mutation tool.

## 11. Recommended PLC tool split

Read-only:

```text
tia_list_plcs
tia_get_plc_overview
tia_list_block_groups
tia_list_blocks
tia_export_block
tia_get_block_metadata
tia_list_tag_tables
tia_list_tags
tia_list_plc_types
tia_find_references
tia_compare_plc_offline_online
```

Validated operations:

```text
tia_compile_plc
tia_compile_block
tia_import_block_preview
tia_validate_external_source
```

Approved mutations:

```text
tia_import_block
tia_create_block
tia_update_tag
tia_import_external_source
tia_delete_block
```

Download tools should remain in a separate, higher-risk capability group.
