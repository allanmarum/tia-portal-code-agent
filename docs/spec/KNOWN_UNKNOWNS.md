# Known Unknowns and Validation Queue

Status: open

Agents must not convert these questions into assumptions.

## KU-001 - Target TIA Portal version

Question:

- Which exact TIA Portal version and build is the first supported target?

Evidence required:

- installed version;
- `PublicAPI` directory;
- Add-In DLL file version;
- Publisher file version;
- framework requirement.

## KU-002 - Exact supported selection types

Question:

- Which Siemens object types can be used for block-level context commands in the target version?

Evidence required:

- installed API inspection;
- minimal provider prototype;
- manual context-menu test.

## KU-003 - Block source access

Question:

- Can the target block content be read directly, or must it be exported?

Evidence required:

- version-specific Openness documentation;
- adapter prototype;
- sample project.

## KU-004 - Reference lookup

Question:

- Is reference or symbol-usage information directly available for required object types?

Evidence required:

- Openness API;
- Openness Explorer;
- export-analysis fallback assessment.

## KU-005 - TIA API threading model

Question:

- Which operations must run on which TIA/Add-In execution context?

Evidence required:

- official version-specific documentation;
- controlled stress test;
- no UI freeze.

## KU-006 - Add-In UI model

Question:

- Does the target version support a preferred embedded panel, or should the product use a separate WPF window?

Evidence required:

- target-version Add-In documentation;
- UX prototype;
- permission impact.

## KU-007 - OpenCode server API

Question:

- Which installed OpenCode version and endpoint schema will be used?

Evidence required:

- installed version;
- official OpenAPI or SDK;
- health/task/progress/cancel prototype.

## KU-008 - MCP transport in Add-In process

Question:

- Which MCP C# SDK and transport are compatible with the Add-In target framework?

Evidence required:

- package target framework;
- dependency compatibility;
- load test inside TIA process.

Fallback:

- external MCP host with named-pipe proxy.

## KU-009 - Package deployment permissions

Question:

- Does installation require administrator privileges in the target workstation policy?

Evidence required:

- target installation folder ACL;
- enterprise deployment policy.

## KU-010 - Digital signing

Question:

- Is Add-In package or assembly signing required by deployment policy?

Evidence required:

- customer security policy;
- Siemens package support;
- Windows application-control policy.

## KU-011 - Compilation side effects

Question:

- What exact project state changes or dialogs can compilation trigger in the target version?

Evidence required:

- version documentation;
- controlled test;
- UI behavior recording.

## KU-012 - Write recovery

Question:

- Which object types support a reliable export/import rollback?

Evidence required:

- object-specific Openness capability;
- round-trip test;
- compilation result.

## KU-013 - Startdrive parameter whitelist

Question:

- Which target parameters are accessible in the deployed Startdrive version?

Evidence required:

- Siemens whitelist;
- target device/version test.

## KU-014 - Licensing

Question:

- Which engineering licenses are installed and required for each implemented operation?

Evidence required:

- target workstation license inventory;
- operation test.

## Validation rule

When resolving an item:

1. attach evidence;
2. record exact version;
4. update affected tasks;
5. create an ADR if the result changes architecture.
