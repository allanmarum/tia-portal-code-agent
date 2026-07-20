# TIA Portal V21 Safety and Safety Validation API

## 1. Scope and policy

Safety APIs are exposed through:

- `Siemens.Engineering.Safety`;
- `Siemens.Engineering.SafetyValidation`;
- `Siemens.Engineering.AddIn.Safety`.

This domain includes configuration and validation of safety-related engineering data. It is not an ordinary CRUD surface.

**Project default:** Safety data is read-only for autonomous agents. Any mutation requires a dedicated capability, explicit user confirmation and domain-specific validation.

## 2. Safety engineering model

Key `Siemens.Engineering.Safety` types include:

- `SafetyAdministration`;
- `SafetySettings` and `GlobalSettings`;
- `AssignmentOfBlockNumbers`;
- `RuntimeGroup` and its composition;
- `SafetySignature` and `SafetySignatureProvider`;
- `SafetyPrintout` and print options;
- Safety-specific download configurations.

`AssignmentOfBlockNumbers` exposes configuration for system-generated safety block ranges, including FB, FC and DB ranges and management mode.

Changing these values can affect generated blocks and project consistency. Such changes MUST NOT be exposed through a generic attribute setter.

## 3. Signatures and traceability

Safety signature types and providers allow access to safety signature information. A read tool SHOULD return:

- signature type;
- relevant identifier/value;
- scope;
- creation or update metadata when available;
- current safety system version;
- whether the project state changed after the signature snapshot.

A safety signature is evidence, not merely a status flag. Preserve it in audit records associated with compile, validation and approved changes.

## 4. Safety compile Add-In workflow

`Siemens.Engineering.AddIn.Safety` provides a workflow extension chain:

```text
SafetyCompileAddInProvider
  -> SafetyCompileWorkflowAddIn
    -> SafetyCompileWorkflowSupport
      -> SafetyCompileWorkflowItem
        -> Execute(objects, SafetyCompileContext)
        -> Rollback(...)
```

The workflow support has initialize/dispose hooks for resources shared during a workflow session.

Rules:

- `Execute` MUST be deterministic for the same input and external state.
- External communication MUST use bounded timeouts.
- Rollback MUST not hide partial failure.
- Workflow results MUST include explicit success/failure state and diagnostic context.
- Do not invoke an LLM inside a compile-critical workflow item.

## 5. Safety Validation model

`Siemens.Engineering.SafetyValidation` includes:

- `SafetyValidationAssistant`;
- `ActivationTest` and composition;
- `SafetyFunction` and composition;
- conditions and condition values;
- device queries;
- trace configuration;
- validation results and test state/validity enums;
- activation-test import and printout support.

`ActivationTest` exposes properties such as:

- author;
- evaluation device name;
- name;
- overall state;
- safety functions;
- available devices.

Validation operations SHOULD return a structured snapshot rather than raw Siemens objects.

## 6. Allowed agent capabilities

Read-only capabilities:

```text
tia_get_safety_overview
tia_get_safety_settings
tia_list_safety_runtime_groups
tia_get_safety_signatures
tia_list_activation_tests
tia_get_activation_test
tia_validate_safety_test_configuration
```

Restricted capabilities:

```text
tia_update_safety_settings
tia_import_activation_test
tia_execute_safety_compile_extension
tia_change_safety_block_ranges
tia_download_safety_program
```

Restricted capabilities MUST be disabled unless the deployment explicitly enables them.

## 7. Mandatory controls for Safety mutation

A Safety mutation requires all of the following:

1. authenticated user with Safety-specific permission;
2. selected project and device confirmed in the UI;
3. immutable before-state snapshot;
4. explicit change plan and diff;
5. explicit approval token;
6. exclusive access and transaction where supported;
7. Safety compile/validation;
8. signature/state comparison;
9. audit record containing user, time, project, device, change and result;
10. no automatic download.

## 8. Error handling

Safety errors MUST preserve the original Siemens exception and workflow/validation messages. A generic "operation failed" message is insufficient for engineering review.

The system MUST distinguish:

- API capability unavailable;
- project not configured for Safety;
- permission/trust failure;
- validation failure;
- compile failure;
- stale state/concurrency conflict;
- external workflow failure;
- user cancellation.
