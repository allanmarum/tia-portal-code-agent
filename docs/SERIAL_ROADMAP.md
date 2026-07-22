# Serial release roadmap

The release-professionalization work is executed as a strict sequence defined in [`.github/serial-roadmap.json`](../.github/serial-roadmap.json).

## Invariants

- One and only one item is `active`.
- All earlier items are `done`.
- All later items are `blocked`.
- A pull request may close only the active issue.
- The same pull request must move the active item to `done` and the immediately following item to `active`.
- Keys, issue numbers, titles, and ordering are immutable during normal transitions.

## Required pull request metadata

```text
Closes #<active-issue-number>
Sequence: REL-XXX
Previous: REL-XXX or none
Next: REL-XXX or RELEASE-COMPLETE
```

The branch must use:

```text
issue/<issue-number>-<sequence-lowercase>-<slug>
```

## Enforcement

The consolidated `.github/workflows/ci.yml` workflow runs `scripts/ci/validate-serial-roadmap.ps1` for every pull request targeting `main`.

When the validator already exists on the base branch, CI executes that base-branch copy rather than the pull request copy. This prevents a normal implementation PR from weakening the validator that evaluates it.

The first roadmap PR uses a documented bootstrap transition: `REL-000` becomes `done`, and `REL-001` becomes the first active item.

## Local check

```powershell
./scripts/ci/validate-serial-roadmap.ps1 -SelfTest
```

See [`CONTRIBUTING.md`](../CONTRIBUTING.md) for the complete contribution contract.
