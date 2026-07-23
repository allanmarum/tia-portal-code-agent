# Rollback Guide

How to restore a previous TIA Portal Code Agent version after a failed or unsatisfactory update.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## How rollback works

Rollback restores a previously installed version by:

1. Reading the `previousVersion` recorded in `current.json`.
2. Setting that version as the active version.
3. Redeploying the corresponding Add-In artifact to TIA Portal.

Side-by-side version storage means the previous version's files are still on disk. No re-download is needed.

## List installed versions

View all installed versions, the active version, and the rollback candidate:

```powershell
tia-agent versions
```

Example output:

```text
TIA Agent Versions (CLI Product v0.2.0)
Active Version:    0.2.0-beta.2
Rollback Version:  0.2.0-beta.1
Update Channel:    beta

Installed Versions:
  - 0.2.0-beta.2 [beta] [active] (Installed: 2026-07-22 21:00:00)
  - 0.2.0-beta.1 [beta] [rollback candidate] (Installed: 2026-07-22 20:00:00)
```

## Roll back to the previous version

```powershell
tia-agent rollback
```

This automatically selects the previous version recorded in `current.json` or the most recently installed alternative.

## Roll back to a specific version

```powershell
tia-agent rollback --version 0.2.0-beta.1
```

The target version must be installed. Use `--force` to skip directory existence checks (not recommended for normal use):

```powershell
tia-agent rollback --version 0.2.0-beta.1 --force
```

## Post-rollback verification

After rollback:

```powershell
tia-agent version
tia-agent doctor
```

Restart services if they were running:

```powershell
tia-agent stop
tia-agent start
```

Restart TIA Portal to pick up the restored Add-In.

## When to use rollback vs. clean reinstall

| Situation | Recommended action |
|---|---|
| Update introduced a regression | `tia-agent rollback` |
| Configuration corruption | `tia-agent doctor` to diagnose, then rollback if needed |
| Want to return to a specific older version | `tia-agent rollback --version <version>` |
| Version directory is missing or corrupted | `tia-agent install --force` (reinstall the version) |
| Want to remove all versions and start fresh | `tia-agent uninstall --all` followed by reinstall |

## Removing old versions

To remove a specific version that is not the active or rollback candidate:

```powershell
tia-agent versions remove 0.2.0-beta.1
```

To remove all versions:

```powershell
tia-agent uninstall --all
```

## Rollback policy

Rollback is supported within the same major version line. Cross-major-version rollback is not guaranteed to be compatible. See [RELEASING.md](RELEASING.md) for the full rollback policy.
