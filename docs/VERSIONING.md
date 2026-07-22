# Versioning Policy

This document is the authoritative product-version policy for TIA Portal Code Agent.

## Product version

The product uses Semantic Versioning 2.0.0:

```text
MAJOR.MINOR.PATCH[-PRERELEASE]
```

Examples:

```text
0.2.0-alpha.1
0.2.0-beta.1
0.2.0-rc.1
0.2.0
1.0.0
```

The canonical version never includes a leading `v`. Git tags add the `v` prefix.

## Version meaning

- `MAJOR`: incompatible changes to supported public contracts, installation layout, configuration schema, CLI behavior, IPC/MCP contracts, or upgrade expectations.
- `MINOR`: backward-compatible functionality or a deliberate prerelease milestone toward the next stable release.
- `PATCH`: backward-compatible defect, security, packaging, documentation, or reliability correction that does not add a new compatibility requirement.

Before `1.0.0`, the project may introduce breaking changes in a minor release, but they must be explicitly documented in release notes and migration guidance. Patch releases remain backward-compatible within the same minor line.

## Release channels

| Channel | Format | Purpose | Stability expectation |
|---|---|---|---|
| Alpha | `X.Y.Z-alpha.N` | Early integration and architecture validation | May be incomplete; migration is not guaranteed between alpha builds |
| Beta | `X.Y.Z-beta.N` | Feature-complete testing and installation validation | No intentional feature additions; compatibility defects may still be fixed |
| Release candidate | `X.Y.Z-rc.N` | Final production candidate | Only release-blocking fixes are expected |
| Stable | `X.Y.Z` | Supported public release | Subject to the support and compatibility policies |

`N` starts at `1` and increases monotonically within a channel. Promotion never reuses an existing version.

Valid progression:

```text
0.2.0-alpha.1 -> 0.2.0-alpha.2 -> 0.2.0-beta.1 -> 0.2.0-rc.1 -> 0.2.0
```

A channel may be skipped when its validation purpose is unnecessary, but stable releases must still satisfy all stable-release gates.

## Git tags

Release tags are immutable annotated tags with this exact format:

```text
vX.Y.Z[-prerelease]
```

Examples:

```text
v0.2.0-beta.1
v0.2.0-rc.1
v0.2.0
```

Tags must point to a commit on `main`. A published tag must never be moved, deleted, or reused. A faulty publication receives a new version.

## Component alignment

The CLI, Bridge, Add-In, MCP host, contracts, installer payload, and release manifest are one product release and share the same product version.

Rules:

1. A release contains one canonical product version.
2. Every shipped first-party component reports that version.
3. Independently versioned protocol or schema revisions may exist, but they do not replace the product version.
4. Mixed product versions are unsupported unless an explicit compatibility matrix says otherwise.
5. A source commit may produce only one public product version.

## Build metadata

SemVer build metadata such as `+sha.abcdef` may be used for local or diagnostic builds, but it is not part of public release tags, package identities, update ordering, or channel selection.

## Version authority

The implementation of the single version source is defined by REL-003. Until then, release documentation and tags must follow this policy without introducing competing version authorities.
