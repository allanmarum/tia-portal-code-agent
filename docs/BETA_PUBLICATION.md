# Beta Publication: v0.2.0-beta.1

This document defines the process for publishing and validating the first public installable beta.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Objective

Publish the first public installable beta and validate the actual GitHub and NuGet artifacts.

## Prerequisites

- All release gates from Issues #53-54 have passed
- Clean-machine validation has been completed
- No unresolved release blockers
- Release runner is provisioned and secure (Issue #47)

## Publication Process

### Step 1: Create Release Tag

Create an immutable annotated tag on the validated `main` commit:

```bash
git tag -a v0.2.0-beta.1 -m "Release v0.2.0-beta.1"
git push origin v0.2.0-beta.1
```

**Important:**
- Tag must point to a commit on `main`
- Tag must be immutable (never moved, deleted, or reused)
- Tag format: `vX.Y.Z[-prerelease]`

### Step 2: Run Consolidated Release Workflow

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

### Step 3: Verify Published Artifacts

After publication, verify all artifacts:

#### GitHub Release

1. Navigate to the GitHub Release page
2. Verify the release is marked as prerelease
3. Download and verify artifacts:
   - `TiaAgent-0.2.0-beta.1.addin` - Add-In package
   - `TiaAgent.Cli.0.2.0-beta.1.nupkg` - CLI NuGet package
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
   signtool verify /pa TiaAgent-0.2.0-beta.1.addin
   ```

#### NuGet Package

1. Verify the package is listed on NuGet.org:
   ```bash
   dotnet package search Industrix.TiaAgent.Cli --prerelease
   ```

2. Verify the package version:
   ```bash
   dotnet tool install --global Industrix.TiaAgent.Cli --version 0.2.0-beta.1 --prerelease
   ```

### Step 4: Installation Validation

Test installation on a clean machine without a repository checkout:

1. Install the CLI:
   ```powershell
   dotnet tool install --global Industrix.TiaAgent.Cli --version 0.2.0-beta.1 --prerelease
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

### Step 5: Document Known Issues

Document any known issues or limitations in the release notes:

1. Review validation results
2. Document any failures or limitations
3. Create follow-up issues for reproducible failures
4. Update release notes with known issues

### Step 6: Publish Release Notes

Ensure release notes include:

1. **Version information** - v0.2.0-beta.1
2. **Release channel** - Beta
3. **Changes** - List of changes since last release
4. **Installation instructions** - How to install from NuGet
5. **Known issues** - Any identified problems
6. **Safety limitations** - Experimental status warning
7. **Compatibility** - TIA Portal V21 requirement
8. **Rollback instructions** - How to rollback if needed
9. **Support** - How to report issues

## Acceptance Criteria Verification

- [ ] Public artifacts match the release commit and version
- [ ] Signature and checksums validate after download
- [ ] `dotnet tool install --global Industrix.TiaAgent.Cli --version 0.2.0-beta.1` succeeds
- [ ] Installation and smoke tests succeed without a source checkout
- [ ] Known issues and safety limitations are visible in release notes
- [ ] Any blocker prevents completion of this issue

## Rollback Instructions

If issues are discovered after publication:

1. **Do not delete the tag or artifacts**
2. Create a new patch release with fixes
3. Document the issue in release notes
4. Update installation instructions if needed

## Validation Checklist

Use this checklist to verify publication:

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
- [ ] TIA Portal integration works (if available)
- [ ] Release notes include known issues
- [ ] Safety limitations are prominent
- [ ] Rollback instructions are documented

## Next Steps

After successful beta publication:

1. Monitor for user feedback
2. Track any issues discovered
3. Prepare for RC publication (Issue #57)
4. Continue stabilization work (Issue #56)
