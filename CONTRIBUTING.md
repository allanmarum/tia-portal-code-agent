# Contributing

## Protected main branch

`main` is protected by the repository ruleset documented in [`docs/maintainers/repository-ruleset.md`](docs/maintainers/repository-ruleset.md).

All changes must be made through a pull request. Direct pushes, force pushes, and deletion of `main` are prohibited. Pull requests must have all required checks passing, all conversations resolved, and the source branch updated with the latest `main` before merge.

Use squash merge only. Merge commits and rebase merges are not part of the repository workflow.

## Serial release roadmap

The release-professionalization roadmap is executed strictly in sequence. The source of truth is [`.github/serial-roadmap.json`](.github/serial-roadmap.json).

At any time:

- exactly one roadmap item is `active`;
- completed items are `done`;
- every later item is `blocked`;
- only the active issue may receive an implementation branch or pull request.

Do not begin a blocked issue, even when it appears independent. The ordering intentionally protects architectural, packaging, installation, and release dependencies.

## Starting work

Before changing the repository:

1. Pull the latest `main`.
2. Open `.github/serial-roadmap.json`.
3. Confirm the intended issue is the single `active` item.
4. Confirm its GitHub issue number and sequence key.
5. Create a branch using:

   ```text
   issue/<issue-number>-<sequence-lowercase>-<slug>
   ```

   Example:

   ```text
   issue/29-rel-001-protect-main
   ```

A branch for a blocked issue is invalid and must not be created.

## Pull request contract

Each implementation PR must:

- target `main`;
- close exactly one issue;
- implement only the active roadmap item;
- update the roadmap atomically from the current item to the next item;
- preserve the order, issue numbers, keys, and titles of all roadmap items;
- use a title beginning with the active sequence key;
- request review from the owners selected by `.github/CODEOWNERS`;
- include these standalone metadata lines:

  ```text
  Closes #<issue-number>
  Sequence: REL-XXX
  Previous: REL-XXX or none
  Next: REL-XXX or RELEASE-COMPLETE
  ```

The branch name, PR metadata, active roadmap item, and issue number must agree.

Do not merge while any review conversation is unresolved. Before merge, update the source branch with the latest `main` and wait for all required checks to run again.

## Required checks

The ruleset requires only checks guaranteed to run on every pull request targeting `main`.

Current required checks:

```text
Serial roadmap gate
Build and test
```

After this pull request merges, maintainers must add `Build and test` as a required check in the repository ruleset without removing `Serial roadmap gate`.

Do not make optional, release-only, matrix-child, or environment-specific checks mandatory.

## Roadmap transition

A valid PR changes only two statuses:

```text
current item: active  -> done
next item:    blocked -> active
```

It also updates:

```text
previous = old current
current  = old next
next     = item after the new current
```

No issue may be skipped, reordered, combined, or activated early.

## Local validation

Run the validator self-test:

```powershell
./scripts/ci/validate-serial-roadmap.ps1 -SelfTest
```

To validate a real transition locally, provide the base and head roadmap files plus PR metadata:

```powershell
./scripts/ci/validate-serial-roadmap.ps1 `
  -BaseRoadmapPath ./base-serial-roadmap.json `
  -HeadRoadmapPath ./.github/serial-roadmap.json `
  -PullRequestBody "Closes #29`n`nSequence: REL-001`nPrevious: REL-000`nNext: REL-002" `
  -PullRequestTitle "[REL-001] Protect main" `
  -BranchName "issue/29-rel-001-protect-main"
```

The consolidated `.github/workflows/ci.yml` workflow runs the same validation for every PR targeting `main`.

## Security policy and reporting

Please do not open public GitHub issues for security vulnerabilities. Follow the security policy documented in [`SECURITY.md`](SECURITY.md) and report vulnerabilities privately via GitHub Security Advisories or maintainer contact.

Authoritative security and safety architecture details are specified in [`docs/spec/SECURITY_MODEL.md`](docs/spec/SECURITY_MODEL.md).

## General checks

Run the relevant repository checks before requesting review:

```powershell
dotnet build TiaAgent.sln
dotnet test TiaAgent.sln
```

Later roadmap issues may refine these commands. Follow the repository documentation and the active issue scope.

