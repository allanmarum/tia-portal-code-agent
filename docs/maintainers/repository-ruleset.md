# Main branch protection ruleset

This document is the maintainer source of truth for the GitHub repository ruleset protecting `main`.

## Ruleset identity

- Name: `Protect main`
- Enforcement status: `Active`
- Target: branches
- Include pattern: `refs/heads/main`
- Bypass list: empty by default

Temporary bypasses must be time-limited, documented in an issue, and removed immediately after recovery. Routine maintenance, release work, bots, and repository administrators are not exempt from the normal pull request path.

## Required rules

Enable the following rules for `main`:

1. Restrict deletions.
2. Block force pushes.
3. Require a pull request before merging.
4. Require all conversations to be resolved before merging.
5. Require the source branch to be up to date with `main` before merging.
6. Require status checks to pass before merging.
7. Block direct updates to `main` by leaving the bypass list empty.

Do not enable a required approval count until the repository has a second active maintainer. `CODEOWNERS` still identifies ownership and prepares the repository for code-owner review requirements later.

## Required status checks

The required checks must match the job names emitted by `.github/workflows/ci.yml` exactly.

Current required checks:

- `Serial roadmap gate`
- `Build and test`

This pull request introduces the consolidated implementation CI job (`Build and test`). After it merges, add that job as the second required check without removing the serial gate. Do not require transient matrix children, optional checks, release jobs, or external checks that are not guaranteed to run on every pull request targeting `main`.

Expected required-check set:

- `Serial roadmap gate`
- `Build and test`

## Pull request merge policy

Repository merge settings:

- Allow squash merging: enabled
- Default commit message for squash merges: pull request title and description
- Allow merge commits: disabled
- Allow rebase merging: disabled
- Automatically delete head branches: enabled

Each pull request must target `main`, reference exactly the active serial-roadmap issue, and include the transition metadata required by `CONTRIBUTING.md`.

## Configuration procedure

A repository administrator configures the ruleset in GitHub:

1. Open **Settings → Rules → Rulesets → New branch ruleset**.
2. Set the name to `Protect main` and enforcement to **Active**.
3. Add target branch pattern `main`.
4. Leave bypass actors empty.
5. Enable deletion restriction and force-push blocking.
6. Require pull requests, conversation resolution, status checks, and branches to be up to date.
7. Add only the required checks listed above.
8. Save the ruleset.
9. Under **Settings → General → Pull Requests**, enable squash merging, disable merge commits and rebase merging, and enable automatic branch deletion.

## Verification

After configuration, verify with a disposable branch:

1. Attempt a direct push to `main`; GitHub must reject it.
2. Open a pull request with a failing serial transition; merge must remain blocked.
3. Resolve the serial transition but leave a conversation unresolved; merge must remain blocked.
4. Make `main` advance after the pull request branch was created; merge must remain blocked until the branch is updated.
5. Confirm force push and branch deletion controls cannot be used on `main`.
6. Confirm the merge button offers squash merge only.

Record any intentional ruleset change in a dedicated governance issue and update this document in the same pull request.
