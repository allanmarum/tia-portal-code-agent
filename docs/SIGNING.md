# Release Artifact Signing Policy and Management

This document defines the mandatory code-signing policy, certificate provisioning, secret handling, signature verification, expiration monitoring, and certificate rotation procedures for TIA Portal Code Agent release artifacts.

---

## 1. Overview and Security Objectives

All TIA Portal Add-In release packages (`.addin` files) distributed via beta, release candidate (RC), or stable release channels MUST be digitally signed and verified prior to publication.

### Key Principles
- **Mandatory Signing:** Prerelease (beta, RC) and stable release artifacts cannot reach publication without valid digital signatures.
- **Automated Signature Verification:** Signature verification is performed automatically during packaging (`pack-addin`) and package verification (`verify-addin`). Release jobs fail immediately if signatures are missing or invalid.
- **Secret Masking:** Private keys, passwords, and PFX base64 payloads are never committed to repository source code, written to disk in plain text, or emitted in CI build logs.
- **Development Separation:** Unsigned or self-signed packaging is permitted strictly in local development environments (`0.0.0-dev`). Release builds (`-RequireSigning` or release versions) strictly require valid signatures.

---

## 2. Signing Architecture and Tooling

Code signing for Siemens TIA Portal Add-Ins (OPC packages) is performed by `OpcSigner` (`tools/OpcSigner`), a dedicated tool built against `System.IO.Packaging` and `System.Security.Cryptography.X509Certificates`.

```text
[TIA Portal Add-In Build]
       │
       ▼
[Siemens Add-In Publisher] ──► Raw (.addin) OPC Package
                                          │
                                          ▼
                                 [OpcSigner Tool] ◄── [Certificate Material]
                                          │             - Store Thumbprint
                                          │             - PFX File / Base64
                                          │             - Password
                                          ▼
                             Signed (.addin) Package
                                          │
                                          ▼
                          [Automated Signature Verify] ──► Fail Fast / Pass
```

### Supported Certificate Sources (In Order of Precedence)
1. **Explicit Thumbprint:** Specified via `--thumbprint` argument or `TIA_SIGNING_CERT_THUMBPRINT` environment variable. Searches the Windows Certificate Store (`CurrentUser\My` or `LocalMachine\My`).
2. **PFX File Path:** Specified via `--pfx` argument or `TIA_SIGNING_CERT_PFX` environment variable, decrypted using `TIA_SIGNING_CERT_PASSWORD` or `--password`.
3. **Base64-Encoded PFX:** Specified via `TIA_SIGNING_CERT_PFX_BASE64` environment variable, decrypted using `TIA_SIGNING_CERT_PASSWORD`.
4. **Development Self-Signed Fallback:** Created dynamically (`CN=TIA Portal Code Agent`) ONLY when `RequireSigning` is false (local dev builds).

---

## 3. Secret Management and Isolation

### Repository Boundaries
- `*.pfx`, `*.p12`, private key files, and plain-text passwords MUST NOT be stored in the repository.
- `.gitignore` explicitly ignores certificate files and signing dumps.

### GitHub Actions Secrets Configuration
For GitHub Actions release workflows (`.github/workflows/release.yml`), signing secrets are configured as encrypted repository or environment secrets:

| Secret Name | Description |
|---|---|
| `TIA_SIGNING_CERT_PFX_BASE64` | Base64-encoded string of the release code-signing PFX certificate |
| `TIA_SIGNING_CERT_PASSWORD` | Password protecting the PFX certificate |
| `TIA_SIGNING_CERT_THUMBPRINT` | (Optional) SHA-1 thumbprint of the certificate installed in the runner store |

 Secrets are injected as environment variables exclusively during the release packaging steps and are automatically masked by GitHub Actions runner log filters.

---

## 4. Automated Signature Verification

Signature verification is enforced at two stages:

1. **Inline Packaging Verification:** `build.ps1 pack-addin -RequireSigning` invokes `OpcSigner verify` on the temporary `.addin` artifact before finalizing the artifact filename.
2. **Post-Packaging Verification:** `build.ps1 verify-addin -RequireSigning` opens the packaged `.addin` OPC container, checks `PackageDigitalSignatureManager.IsSigned`, and executes `ValidateSignatures()` to verify signature integrity and certificate validity.

### Command Line Verification
To manually verify an Add-In package signature:
```powershell
.\tools\OpcSigner\bin\Release\net48\OpcSigner.exe verify artifacts\TiaAgent-0.2.0.addin
```

Expected output:
```text
[PASS] Package signature verification succeeded for: artifacts\TiaAgent-0.2.0.addin
  Signer: CN=Company Code Signing CA [THUMBPRINT]
```

---

## 5. Certificate Lifecycle, Expiration Monitoring, and Rotation

### Expiration Monitoring
Code-signing certificates MUST be audited quarterly:
- Inspect certificate expiration dates in the release runner store or secret vault:
  ```powershell
  Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.EnhancedKeyUsageList -match "Code Signing" }
  ```
- Configure automated alerts 60 days prior to certificate expiration.

### Certificate Rotation Procedure

When rotating a code-signing certificate:

1. **Provision New Certificate:**
   - Obtain an updated Code Signing certificate (PFX) from the corporate PKI or recognized Certificate Authority (CA).
   - Verify that the certificate contains the `Code Signing` Enhanced Key Usage (EKU `1.3.6.1.5.5.7.3.3`).

2. **Update Release Runner Store / Secrets:**
   - Encode the new PFX to base64:
     ```powershell
     [Convert]::ToBase64String([System.IO.File]::ReadAllBytes("path\to\new-cert.pfx"))
     ```
   - Update the GitHub Repository secret `TIA_SIGNING_CERT_PFX_BASE64` and `TIA_SIGNING_CERT_PASSWORD`.
   - If using the Windows Certificate Store on the runner, import the new PFX into `Cert:\CurrentUser\My` or `Cert:\LocalMachine\My` and update `TIA_SIGNING_CERT_THUMBPRINT`.

3. **Validate Rotation in Staging / Prerelease:**
   - Execute a test release build:
     ```powershell
     .\build.ps1 pack-addin -RequireSigning
     .\build.ps1 verify-addin -RequireSigning
     ```
   - Confirm that signature verification passes with the new certificate thumbprint.

4. **Revoke Retired Certificate:**
   - Once the new certificate is validated, revoke or archive the old private key according to corporate PKI procedures.

---

## 6. Local Development vs. Release Packaging

| Mode | Command | RequireSigning | Behavior |
|---|---|---|---|
| Local Dev | `.\build.ps1 pack-addin` | `false` | Signs with local self-signed cert or leaves package unsigned if tool absent. |
| Release Build | `.\build.ps1 pack-addin -RequireSigning` | `true` | Requires valid cert material. Fails fast if unsigned or if verification fails. |
| Release Workflow | GitHub Actions `release.yml` | `true` | Enforces mandatory signing and verification for tag releases. |
