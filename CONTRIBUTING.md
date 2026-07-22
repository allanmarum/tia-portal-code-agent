# Contributing

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
- include these standalone metadata lines:

  ```text
  Closes #<issue-number>
  Sequence: REL-XXX
  Previous: REL-XXX or none
  Next: REL-XXX or RELEASE-COMPLETE
  ```

The branch name, PR metadata, active roadmap item, and issue number must agree.

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

## General checks

Run the relevant repository checks before requesting review:

```powershell
dotnet build TiaAgent.sln
dotnet test TiaAgent.sln
```

Later roadmap issues may refine these commands. Follow the repository documentation and the active issue scope.
