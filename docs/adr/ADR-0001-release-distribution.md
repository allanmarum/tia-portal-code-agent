# ADR-0001: Release and Distribution Architecture

- Status: Accepted
- Date: 2026-07-22
- Decision owners: TIA Portal Code Agent maintainers
- Related issue: #30 (REL-002)

## Context

TIA Portal Code Agent contains multiple cooperating first-party components, including the CLI, Bridge, Add-In, MCP/runtime integration, contracts, configuration, and installation assets. Independent component versions would make installation, support, diagnostics, upgrades, and rollback ambiguous.

The project also needs prerelease channels, immutable release identities, explicit Siemens TIA Portal compatibility, and a repository flow that prevents divergent release branches or artifacts built from unknown commits.

This ADR defines policy before later roadmap items implement version properties, packaging, installers, update channels, signing, and publication workflows.

## Decision

### One product, one version

All shipped first-party components form one product release and share one Semantic Versioning product version.

Component-specific protocol or schema revisions may exist for compatibility negotiation, but they are subordinate metadata and do not create independently released products.

### Semantic Versioning and channels

The canonical version is `MAJOR.MINOR.PATCH[-PRERELEASE]`. Supported prerelease identifiers are:

```text
alpha.N
beta.N
rc.N
```

Stable releases have no prerelease suffix. Public Git tags use `vX.Y.Z[-prerelease]` and are immutable.

### Trunk-based release source

`main` is the only long-lived development branch. Short-lived issue branches merge through protected pull requests using squash merge. Every public release tag points to a commit on `main`.

Prerelease channels are represented by versions and tags, not by permanent alpha, beta, RC, or release branches.

### Distribution unit

A release is an atomic, verifiable distribution set produced from one tagged commit. It is expected to include, as applicable:

- CLI package;
- Bridge and runtime executables;
- TIA Portal Add-In payload;
- configuration examples and schemas;
- notices and license material;
- checksums, SBOM, signatures, and release manifest;
- release notes and compatibility declaration.

The exact implementation and package layout are deferred to later roadmap issues.

### Compatibility identity

The product version is independent from TIA Portal compatibility. Releases declare supported TIA Portal and Openness versions in a compatibility matrix. The current baseline is TIA Portal V21 / Openness V21.

### Upgrade and rollback

Stable releases must declare supported upgrade sources and rollback guarantees. Rollback means atomically reactivating a previously complete, compatible product version and configuration. Downgrade is unsupported unless explicitly declared.

Irreversible migrations require advance disclosure and a backup or clean-reinstall path.

### Publication integrity

Published tags and artifacts are immutable. A faulty release is superseded by a new version; existing release identities are never silently replaced.

## Consequences

### Positive

- One version is sufficient for user support and diagnostics.
- CLI, Bridge, and Add-In compatibility is unambiguous.
- Tags, packages, release notes, and installed manifests can refer to one release identity.
- Prerelease promotion is explicit and ordered.
- TIA Portal support can evolve without corrupting product SemVer.
- Side-by-side installation and rollback can be designed around complete release units.

### Negative

- A change to any shipped component may require releasing the entire product set.
- Components cannot publish arbitrary independent public versions.
- Release automation must enforce alignment across all artifacts.
- Compatibility validation must cover the integrated distribution, not only individual projects.

## Alternatives considered

### Independently version every component

Rejected because it creates a compatibility matrix between CLI, Bridge, Add-In, contracts, and installer before the product has a need for independent lifecycles.

### Encode TIA Portal version in product SemVer

Rejected because Siemens compatibility and product evolution are separate dimensions. Encoding V21 into SemVer would make product release ordering and future multi-version support unclear.

### Maintain release and channel branches

Rejected for normal development because long-lived branches diverge from the strictly serial roadmap and increase backport and publication complexity. Immutable tags on protected `main` provide a simpler source of truth.

### Replace faulty artifacts under the same tag

Rejected because it destroys reproducibility, auditability, and update integrity.

## Implementation constraints

Later roadmap items must conform to this ADR:

- REL-003 creates a single product-version source.
- Packaging and installation must preserve version alignment.
- Diagnostics must report product, component, protocol/schema, TIA Portal, and Openness versions separately.
- Release workflows must build and publish from immutable tags on `main`.
- Update and rollback mechanisms must operate on complete release units.

Any change to these decisions requires a superseding ADR.