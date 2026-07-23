# Release Candidate Publication: v0.2.0-rc.1

This document defines the process for publishing and validating the release candidate.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Objective

Publish a feature-frozen release candidate and validate it as the exact build intended for stable release.

## Prerequisites

- Beta publication (Issue #55) completed and validated
- Hardening improvements (Issue #56) implemented and tested
- No unresolved release blockers or critical defects
- All supported runtime adapters pass smoke validation
- Security posture reviewed and documented

## Publication Process

### Step 1: Feature Freeze

Before creating the RC tag:

1. **Freeze new feature work** - No new features after this point
2. **Review open issues** - Ensure no blockers or critical defects remain
3. **Validate beta** - Confirm beta publication is stable
4. **Review hardening** - Confirm hardening improvements are complete

### Step 2: Create Release Candidate Tag

Create an immutable annotated tag on the validated `main` commit:

```bash
git tag -a v0.2.0-rc.1 -m "Release Candidate v0.2.0-rc.1"
git push origin v0.2.0-rc.1
```

**Important:**
- Tag must point to a commit on `main`
- Tag must be immutable (never moved, deleted, or reused)
- Tag format: `vX.Y.Z-rc.N`

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
2. Verify the release is marked as prerelease
3. Download and verify artifacts:
   - `TiaAgent-0.2.0-rc.1.addin` - Add-In package
   - `TiaAgent.Cli.0.2.0-rc.1.nupkg` - CLI NuGet package
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
   signtool verify /pa TiaAgent-0.2.0-rc.1.addin
   ```

#### NuGet Package

1. Verify the package is listed on NuGet.org:
   ```bash
   dotnet package search Industrix.TiaAgent.Cli --prerelease
   ```

2. Verify the package version:
   ```bash
   dotnet tool install --global Industrix.TiaAgent.Cli --version 0.2.0-rc.1 --prerelease
   ```

### Step 5: Installation Validation

Test installation on a clean machine:

1. Install the CLI:
   ```powershell
   dotnet tool install --global Industrix.TiaAgent.Cli --version 0.2.0-rc.1 --prerelease
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

### Step 6: Beta-to-RC Upgrade Validation

Test upgrade from beta to RC:

1. Start with beta version installed
2. Update to RC:
   ```powershell
   dotnet tool update --global Industrix.TiaAgent.Cli --version 0.2.0-rc.1 --prerelease
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

### Step 7: RC Rollback Validation

Test rollback from RC to previous version:

1. Start with RC version installed
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

### Step 9: Security and Documentation Review

Review security posture and documentation:

1. **Security review** - Verify security measures are in place
2. **Documentation review** - Ensure all documentation is accurate
3. **Compatibility metadata** - Verify compatibility declarations
4. **Signing** - Verify release signing is working
5. **SBOM** - Verify Software Bill of Materials is complete
6. **Checksums** - Verify checksums are correct
7. **Release notes** - Verify release notes are complete

### Step 10: Document Known Issues

Document any known issues or limitations:

1. Review validation results
2. Document any failures or limitations
3. Create follow-up issues for reproducible failures
4. Update release notes with known issues

### Step 11: Publish Release Notes

Ensure release notes include:

1. **Version information** - v0.2.0-rc.1
2. **Release channel** - Release Candidate
3. **Changes** - List of changes since beta
4. **Installation instructions** - How to install from NuGet
5. **Upgrade instructions** - How to upgrade from beta
6. **Known issues** - Any identified problems
7. **Safety limitations** - Experimental status warning
8. **Compatibility** - TIA Portal V21 requirement
9. **Rollback instructions** - How to rollback if needed
10. **Support** - How to report issues

## Acceptance Criteria Verification

- [ ] v0.2.0-rc.1 is published as a prerelease
- [ ] Clean installation and beta-to-RC update succeed
- [ ] Rollback from RC to the previous known-good version succeeds
- [ ] All supported runtime adapters pass smoke validation
- [ ] No blocker or critical defect remains open
- [ ] Stable release requires no code change other than approved RC fixes and version/tag publication

## Validation Checklist

Use this checklist to verify publication:

- [ ] Feature freeze implemented
- [ ] No unresolved blockers or critical defects
- [ ] Tag created on validated `main` commit
- [ ] Release workflow completed successfully
- [ ] GitHub Release created as prerelease
- [ ] All artifacts published to GitHub Release
- [ ] NuGet package published to NuGet.org
- [ ] Checksums verified
- [ ] Signature verified (if applicable)
- [ ] Installation from NuGet succeeds
- [ ] Payload installation succeeds
- [ ] Doctor reports healthy environment
- [ ] Services start and respond to health checks
- [ ] Beta-to-RC upgrade succeeds
- [ ] RC rollback succeeds
- [ ] All runtime adapters pass validation
- [ ] TIA Portal integration works (if available)
- [ ] Security posture reviewed
- [ ] Documentation reviewed and accurate
- [ ] Release notes include known issues
- [ ] Safety limitations are prominent
- [ ] Rollback instructions are documented

## Next Steps

After successful RC publication:

1. Monitor for user feedback
2. Track any issues discovered
3. Prepare for stable publication (Issue #58)
4. Only approved RC fixes may produce another RC
