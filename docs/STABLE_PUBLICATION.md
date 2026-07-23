# Stable Publication: v0.2.0

This document defines the process for publishing the stable release.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Objective

Complete the implementation, validation, and stabilization work required to make version 0.2.0 eligible for a future stable release.

## Prerequisites

- Release candidate (Issue #57) completed and validated
- All release blockers and critical regressions resolved
- Complete validation suite passed
- No unresolved release blockers

## Release Gates

Before publishing v0.2.0 as stable, all of the following gates must pass:

### Gate 1: Release Blockers Resolved

- [ ] All release blockers are resolved
- [ ] All critical regressions are resolved
- [ ] No unresolved issues remain open
- [ ] All acceptance criteria are met

### Gate 2: Release Candidate Validated

- [ ] v0.2.0-rc.1 is published and validated
- [ ] Clean installation succeeds
- [ ] Doctor reports healthy environment
- [ ] Startup and runtime selection work correctly
- [ ] TIA Portal integration and round trip are validated
- [ ] Upgrade from prerelease path works
- [ ] Rollback works correctly

### Gate 3: Artifact Validation

- [ ] Release artifacts validate against signatures
- [ ] Release artifacts validate against checksums
- [ ] Release artifacts validate against SBOM
- [ ] Release artifacts validate against manifest
- [ ] All artifacts are complete and correct

### Gate 4: Documentation Complete

- [ ] Release notes clearly state safety limits
- [ ] Release notes clearly state compatibility
- [ ] Release notes clearly state known issues
- [ ] Release notes clearly state installation procedures
- [ ] Release notes clearly state update procedures
- [ ] Release notes clearly state rollback procedures
- [ ] Release notes clearly state support procedures

### Gate 5: Publication Ready

- [ ] Only after all criteria above pass
- [ ] GitHub may publish 0.2.0 as stable
- [ ] NuGet may publish 0.2.0 as stable
- [ ] `dotnet tool install --global Industrix.TiaAgent.Cli` resolves the stable version

## Publication Process

### Step 1: Final Validation

Before creating the stable tag:

1. **Complete validation suite** - Run all tests and validations
2. **Review release notes** - Ensure all required information is included
3. **Verify artifacts** - Confirm all artifacts are ready for publication
4. **Confirm no blockers** - Ensure no issues remain open

### Step 2: Create Stable Tag

Create an immutable annotated tag on the validated `main` commit:

```bash
git tag -a v0.2.0 -m "Stable Release v0.2.0"
git push origin v0.2.0
```

**Important:**
- Tag must point to a commit on `main`
- Tag must be immutable (never moved, deleted, or reused)
- Tag format: `vX.Y.Z`

### Step 3: Run Consolidated Release Workflow

The tag push triggers the consolidated release workflow (`.github/workflows/release.yml`):

1. **Validate tag** - Ensures tag format is correct
2. **Build and test** - Compiles solution and runs all tests
3. **Package Add-In** - Creates `.addin` OPC package
4. **Package CLI** - Creates NuGet package with payload
5. **Sign artifacts** - Applies release signing
6. **Generate release metadata** - Creates checksums, SBOM, release manifest
7. **Verify artifacts** - Validates all packages
8. **Publish to GitHub** - Creates GitHub Release with artifacts
9. **Publish to NuGet** - Pushes CLI package to NuGet.org

### Step 4: Verify Published Artifacts

After publication, verify all artifacts:

#### GitHub Release

1. Navigate to the GitHub Release page
2. Verify the release is marked as stable (not prerelease)
3. Download and verify artifacts:
   - `TiaAgent-0.2.0.addin` - Add-In package
   - `TiaAgent.Cli.0.2.0.nupkg` - CLI NuGet package
   - `release-manifest.json` - Release manifest
   - `SHA256SUMS` - Checksums file
   - `sbom.spdx.json` - Software Bill of Materials
   - `THIRD_PARTY_NOTICES.md` - Third-party notices

4. Verify checksums:
   ```bash
   sha256sum -c SHA256SUMS
   ```

5. Verify signature (if signing is configured):
   ```bash
   # Verify Add-In signature
   signtool verify /pa TiaAgent-0.2.0.addin
   ```

#### NuGet Package

1. Verify the package is listed on NuGet.org:
   ```bash
   dotnet package search Industrix.TiaAgent.Cli
   ```

2. Verify the package resolves as stable:
   ```bash
   dotnet tool install --global Industrix.TiaAgent.Cli
   ```

### Step 5: Installation Validation

Test installation on a clean machine:

1. Install the CLI:
   ```powershell
   dotnet tool install --global Industrix.TiaAgent.Cli
   ```

2. Verify installation:
   ```powershell
   tia-agent --help
   tia-agent version
   ```

3. Install payload:
   ```powershell
   tia-agent install
   ```

4. Run diagnostics:
   ```powershell
   tia-agent doctor
   ```

5. Start services:
   ```powershell
   tia-agent start
   ```

6. Verify health:
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:43119/health"
   ```

7. Test TIA Portal integration (if TIA Portal is available)

8. Stop services:
   ```powershell
   tia-agent stop
   ```

### Step 6: Upgrade Validation

Test upgrade from prerelease to stable:

1. Start with RC version installed
2. Update to stable:
   ```powershell
   dotnet tool update --global Industrix.TiaAgent.Cli
   tia-agent update
   ```

3. Verify update:
   ```powershell
   tia-agent version
   ```

4. Run diagnostics:
   ```powershell
   tia-agent doctor
   ```

### Step 7: Rollback Validation

Test rollback from stable to previous version:

1. Start with stable version installed
2. Rollback to previous version:
   ```powershell
   tia-agent rollback
   ```

3. Verify rollback:
   ```powershell
   tia-agent version
   ```

4. Run diagnostics:
   ```powershell
   tia-agent doctor
   ```

### Step 8: Runtime Adapter Validation

Validate all supported runtime adapters:

1. Test OpenCode adapter:
   ```powershell
   tia-agent runtime doctor opencode
   ```

2. Test Mimo adapter:
   ```powershell
   tia-agent runtime doctor mimo
   ```

3. Test Claude Code adapter:
   ```powershell
   tia-agent runtime doctor claude
   ```

4. Test each adapter with a simple task

### Step 9: Documentation Review

Review all documentation:

1. **Release notes** - Verify all required information is included
2. **Installation guide** - Verify installation procedures are accurate
3. **Update guide** - Verify update procedures are accurate
4. **Rollback guide** - Verify rollback procedures are accurate
5. **Troubleshooting guide** - Verify troubleshooting information is accurate
6. **Compatibility** - Verify compatibility declarations are accurate
7. **Security** - Verify security information is accurate

### Step 10: Publish Release Notes

Ensure release notes include:

1. **Version information** - v0.2.0
2. **Release channel** - Stable
3. **Changes** - List of all changes since initial development
4. **Installation instructions** - How to install from NuGet
5. **Upgrade instructions** - How to upgrade from prerelease
6. **Known issues** - Any identified problems
7. **Safety limitations** - Experimental status warning
8. **Compatibility** - TIA Portal V21 requirement
9. **Rollback instructions** - How to rollback if needed
10. **Support** - How to report issues

### Step 11: Close Serial Roadmap

After successful publication:

1. **Mark REL-030 as done** - Update serial roadmap
2. **Mark RELEASE-COMPLETE as done** - Close the serial roadmap
3. **Create post-release backlog** - Create separate issues for future work
4. **Document completion** - Record that the serial roadmap is complete

## Acceptance Criteria Verification

- [ ] All release blockers and critical regressions are resolved
- [ ] The approved release candidate passes the complete validation suite
- [ ] `tia-agent install`, `doctor`, and `start` succeed on a supported clean environment
- [ ] TIA Portal integration and the complete TIA → agent runtime → TIA round trip are validated
- [ ] Upgrade from the approved prerelease path and rollback are validated
- [ ] Release artifacts validate against signatures, checksums, SBOM, and manifest
- [ ] Release notes clearly state safety limits, compatibility, known issues, installation, update, rollback, and support
- [ ] Only after all criteria above pass, GitHub and NuGet may publish 0.2.0 as stable rather than prerelease
- [ ] Only after approval, `dotnet tool install --global Industrix.TiaAgent.Cli` resolves the stable version
- [ ] The serial roadmap records REL-030 as complete with no next active issue

## Validation Checklist

Use this checklist to verify publication:

- [ ] All release blockers resolved
- [ ] All critical regressions resolved
- [ ] Release candidate validated
- [ ] Complete validation suite passed
- [ ] No unresolved issues remain open
- [ ] Tag created on validated `main` commit
- [ ] Release workflow completed successfully
- [ ] GitHub Release created as stable
- [ ] All artifacts published to GitHub Release
- [ ] NuGet package published to NuGet.org
- [ ] Checksums verified
- [ ] Signature verified (if applicable)
- [ ] Installation from NuGet succeeds
- [ ] Payload installation succeeds
- [ ] Doctor reports healthy environment
- [ ] Services start and respond to health checks
- [ ] Upgrade from prerelease succeeds
- [ ] Rollback to previous version succeeds
- [ ] All runtime adapters pass validation
- [ ] TIA Portal integration works (if available)
- [ ] Documentation reviewed and accurate
- [ ] Release notes include all required information
- [ ] Safety limitations are prominent
- [ ] Rollback instructions are documented
- [ ] Serial roadmap marked as complete

## Post-Release

After successful stable publication:

1. **Monitor user feedback** - Track any issues discovered
2. **Create post-release backlog** - Create separate issues for future work
3. **Plan next release** - Begin planning for v0.2.1 or v0.3.0
4. **Document lessons learned** - Record any process improvements

## Serial Roadmap Completion

After v0.2.0 is published:

1. Update `.github/serial-roadmap.json`:
   - Mark REL-030 as done
   - Mark RELEASE-COMPLETE as done

2. Create a commit:
   ```bash
   git add .github/serial-roadmap.json
   git commit -m "chore: mark serial roadmap complete

   The v0.2.0 stable release has been published. The serial roadmap
   is now complete. Future work will be tracked in separate issues."
   ```

3. Push and merge:
   ```bash
   git push origin main
   ```

4. Close any remaining issues
5. Create post-release backlog for future work
