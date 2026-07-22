## Serial metadata

Closes #<active-issue-number>

Sequence: REL-XXX
Previous: REL-XXX-or-none
Next: REL-XXX-or-RELEASE-COMPLETE

## What changed

Describe the implementation completed for the active serial roadmap issue.

## Why

Explain the problem addressed and why this change is limited to the active issue.

## Validation

- [ ] I ran the relevant local checks.
- [ ] I ran `./scripts/ci/validate-serial-roadmap.ps1 -SelfTest`.
- [ ] This PR advances `.github/serial-roadmap.json` by exactly one item.
- [ ] No work from a blocked future issue is included.
- [ ] The branch is current with `main`.
- [ ] All review conversations are resolved.
- [ ] The PR is ready to use squash merge only.
- [ ] Any required maintainer or `CODEOWNERS` review has been requested.

## Required checks

- [ ] `Serial roadmap gate` passes.
- [ ] `Build and test` passes.

## Notes

Document known limitations, follow-up work, manual repository settings, or validation requirements.
