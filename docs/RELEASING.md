# Release Policy

This document defines how changes reach a TIA Portal Code Agent release. It specifies policy only; workflow automation is implemented by later roadmap items.

## Development model

The repository uses trunk-based development with protected `main` and short-lived branches.

- `main` is the only long-lived development branch and must remain releasable.
- Work is performed on the single branch allowed by `.github/serial-roadmap.json`.
- Branch names follow `issue/<issue>-<sequence-lowercase>-<slug>`.
- Changes enter `main` through reviewed pull requests using squash merge.
- Release branches are not used for normal development.
- Published releases are identified by immutable tags on `main`.

Long-running stabilization work must remain exceptional. A release candidate is represented by a tag, not by a permanent branch.

## Release channels

### Alpha

Alpha releases validate incomplete integration, packaging, installation, and architectural decisions. They may omit planned capabilities and may require a clean installation.

### Beta

Beta begins when the target feature set is complete. Beta changes are limited to defects, security, compatibility, installation, documentation, and validation findings. New scope requires an explicit roadmap decision.

### Release candidate

An RC is a build believed suitable for stable publication. Only release-blocking corrections may produce another RC.

### Stable

A stable release is the supported distribution for general use. It must have complete artifacts, checksums, release notes, installation guidance, compatibility declarations, and rollback information.

## Release preparation

For every release:

1. Select the version according to `docs/VERSIONING.md`.
2. Confirm all first-party components resolve to the same product version.
3. Confirm the target commit is on `main` and all required checks passed.
4. Confirm the declared TIA Portal compatibility in `docs/COMPATIBILITY.md`.
5. Build, test, package, sign, and verify using the consolidated release workflow (`.github/workflows/release.yml`).
6. Generate release notes covering changes, compatibility, known limitations, upgrade steps, and rollback.
7. Create the immutable annotated tag `vX.Y.Z[-prerelease]`.
8. Publish all artifacts from that exact tagged commit.
9. Validate installation and reported versions from the published artifacts.

A failed publication is corrected with a new version. Tags and released artifacts are never silently replaced.

## Breaking changes

A change is breaking when an existing supported installation cannot continue without manual migration or when a supported public contract changes incompatibly. Examples include:

- CLI command or exit-code incompatibility;
- configuration key removal or semantic change;
- IPC, MCP, HTTP, manifest, or schema incompatibility;
- installed path or activation behavior that invalidates an existing installation;
- removal of a supported TIA Portal version;
- an upgrade that cannot preserve or restore user configuration.

Breaking changes require:

- a MAJOR version at or after `1.0.0`;
- a MINOR version before `1.0.0`;
- explicit migration and rollback documentation;
- prominent release-note disclosure;
- validation from the oldest supported upgrade source.

## Support policy

- Stable releases are the only generally supported channel.
- Prereleases are supported for evaluation and defect reporting, not production guarantees.
- The latest stable minor line receives normal fixes.
- Security or critical reliability fixes may be backported when maintainers explicitly designate a supported older line.
- Support for a version includes only the TIA Portal and operating-system combinations listed in the compatibility matrix.

No indefinite support period is implied. A release may be declared end-of-support in release notes or maintained support documentation, with a recommended upgrade target.

## Upgrade policy

Within a stable major line, upgrades must preserve supported configuration and user-managed data or provide an automated, documented migration.

Each stable release must define:

- supported source versions;
- whether in-place upgrade is supported;
- configuration or schema migrations;
- service/process shutdown requirements;
- verification after upgrade;
- rollback prerequisites.

Skipping versions is supported only when the target release explicitly states that the source version is accepted.

## Downgrade and rollback policy

Downgrade is not assumed to be safe. Rollback means restoring the previously active complete product version and its compatible configuration/state.

A release must not claim rollback support unless it can:

1. retain or restore the previous version's artifacts;
2. restore compatible configuration and manifests;
3. stop processes from the failed version;
4. atomically reactivate the prior version;
5. verify the prior version after activation.

If a migration is irreversible, the release notes must state this before installation and require a backup or clean reinstall path.

## Hotfixes

A stable hotfix uses a PATCH release from a fix merged through `main`. The release is tagged from `main`; no unreviewed artifact may be published from a local or detached commit.

## Release ownership

Maintainers approve channel promotion and stable publication. Automation may enforce gates but does not replace the explicit decision that a release is ready.