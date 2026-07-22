# Compatibility Policy

Product version and Siemens TIA Portal compatibility are independent dimensions.

```text
Product version: TIA Portal Code Agent release, for example 0.2.0-beta.1
TIA compatibility: validated Siemens environment, for example TIA Portal V21 / Openness V21
```

A product version must not encode the TIA Portal version in its SemVer number.

## Current compatibility baseline

The current engineering and validation baseline is:

| Dimension | Baseline |
|---|---|
| TIA Portal | V21 |
| TIA Portal Openness | V21 Public API |
| Add-In target | TIA Portal V21 |
| Host operating system | Windows versions supported by the selected TIA Portal V21 installation |

This baseline is a policy declaration, not evidence that every V21 edition, update, language pack, hardware catalog, or project type has been tested.

## Compatibility matrix requirements

Every public release must publish a matrix containing at least:

- product version;
- release channel;
- TIA Portal major version and tested update level;
- Openness API version;
- supported Add-In host version;
- supported operating-system baseline;
- installation mode;
- known limitations;
- validation status: `supported`, `experimental`, or `unsupported`.

Definitions:

- `supported`: validated by the release process and eligible for normal defect support;
- `experimental`: expected to work but not fully validated; no production guarantee;
- `unsupported`: intentionally blocked, known incompatible, or outside the tested boundary.

## Component compatibility

CLI, Bridge, Add-In, MCP host, contracts, and installer payload from one release are version-aligned and form one supported unit.

- Mixing components from different product versions is unsupported by default.
- A protocol handshake must reject known-incompatible versions rather than continue silently.
- Protocol or manifest schema versions may evolve independently, but each product release must declare which revisions it implements.
- Diagnostics should report product version, component version, protocol/schema revision, TIA Portal version, and Openness version separately.

## TIA Portal support changes

Adding support for another TIA Portal major version is a compatibility feature and normally requires a MINOR release.

Removing a previously supported TIA Portal major version is a breaking change:

- before `1.0.0`, it requires a MINOR release and explicit migration guidance;
- at or after `1.0.0`, it requires a MAJOR release unless the removed version was already formally end-of-support.

A newer TIA Portal version is not automatically supported merely because assemblies load or basic operations succeed.

## Update levels

Compatibility should be validated against explicit TIA Portal update levels when Siemens updates may affect Add-In or Openness behavior. When only the major version is declared, the release notes must identify the exact environment used for validation.

## Project compatibility

Compatibility with TIA Portal does not imply compatibility with every project feature. Release notes must identify limitations involving, where relevant:

- Safety projects;
- WinCC or WinCC Unified;
- hardware and network configuration;
- protected or know-how-protected blocks;
- multiuser or Teamcenter workflows;
- project upgrades from older TIA versions;
- download to controllers;
- unsupported object types in Openness.

Unsupported operations must fail explicitly and must not degrade into unsafe best-effort writes.

## Upgrade compatibility

A supported in-place upgrade requires both:

1. product-version compatibility as defined in `docs/RELEASING.md`; and
2. compatibility with the installed TIA Portal/Openness environment.

Changing the TIA Portal major version and the product version simultaneously should be treated as a higher-risk migration and validated as a separate scenario.

## Evidence and claims

Compatibility claims must be based on repeatable validation, not inference. Documentation should distinguish:

- officially validated behavior;
- behavior inherited from Siemens documentation;
- experimental observations;
- unsupported assumptions.

Future roadmap work may automate matrix generation and diagnostics, but those implementations must conform to this policy.