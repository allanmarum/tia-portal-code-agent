# Provisioning and Securing the Windows Release Runner

This document defines the architecture, setup procedure, security model, isolation mechanisms, maintenance, and disaster recovery for the dedicated Windows self-hosted release runner used by TIA Portal Code Agent.

## Overview

TIA Portal Code Agent requires a Windows x64 environment equipped with Siemens TIA Portal V21, Siemens Add-In Publisher (`Siemens.Engineering.AddIn.Publisher.exe`), .NET Framework 4.8 Developer Pack, and .NET SDK 8.0 to produce, package, sign, and verify release artifacts.

To protect build integrity, secrets, and code-signing assets:
- Release packaging and publishing jobs run on dedicated Windows self-hosted runners.
- Pull request CI workflows (`.github/workflows/ci.yml`) run exclusively on GitHub-hosted `windows-latest` runners.
- Self-hosted runners are restricted to tag-driven release workflows on protected commits.

---

## Runner Identity and Labels

Self-hosted release runners must be registered with the following labels:

| Label | Purpose |
|---|---|
| `self-hosted` | Identifies a self-hosted GitHub Actions runner |
| `Windows` | Specifies the Windows operating system |
| `x64` | Specifies 64-bit architecture |
| `tia-v21` | Indicates Siemens TIA Portal V21 Openness & Publisher availability |
| `release-runner` | Restricts runner usage to authorized release workflows |

Example workflow job target specification:
```yaml
runs-on: [self-hosted, Windows, x64, tia-v21, release-runner]
```

---

## Hardware and Software Prerequisites

### Hardware Requirements
- **OS:** Windows 10/11 Pro/Enterprise x64 (or Windows Server 2022 x64)
- **CPU:** 4 cores minimum (8 cores recommended for TIA Portal compilation)
- **RAM:** 16 GB minimum (32 GB recommended)
- **Storage:** 100 GB available SSD storage

### Software Prerequisites
1. **Siemens TIA Portal V21:**
   - Standard installation at `%ProgramFiles%\Siemens\Automation\Portal V21`
   - Openness API assemblies located at `%ProgramFiles%\Siemens\Automation\Portal V21\PublicAPI\V21\net48\`
   - Siemens Add-In Publisher located at `%ProgramFiles%\Siemens\Automation\Portal V21\PublicAPI\V21.AddIn\Siemens.Engineering.AddIn.Publisher.exe`
2. **Developer Tooling:**
   - .NET SDK `8.0.423` (or pinned version per `global.json`)
   - .NET Framework 4.8 Developer Pack
   - Git 2.40+ for Windows
   - PowerShell 7.4+ (`pwsh`)
3. **Windows Group Membership:**
   - The runner service account must belong to the local Windows group `Siemens TIA Openness`.

---

## Service Account and Security Model

### Least-Privilege Account
- The GitHub Actions runner service runs under a dedicated, non-administrator Windows service account (`tia-runner`).
- The account is granted filesystem permissions strictly limited to the runner directory (`C:\actions-runner`) and `%LOCALAPPDATA%\TiaAgent`.
- The account must NOT belong to the `Administrators` group.

### Secret and Credential Handling
- Signing certificates, private keys, and API tokens are NEVER stored in the repository, workspace, or build logs.
- Code-signing keys are managed via the Windows Certificate Store or hardware security modules (HSM) accessible only to the `tia-runner` account.
- Build scripts mask secrets from standard output and standard error streams.
- Secret environment variables injected by GitHub Actions are retained in memory for the duration of the job step and cleared immediately after.

### Network Isolation
- Local services (Bridge, MCP, test HTTP endpoints) bind strictly to `127.0.0.1` (loopback).
- External network outbound access is restricted via Windows Firewall to GitHub API (`github.com`), NuGet gallery (`api.nuget.org`), and required CRL/OCSP signing verification endpoints.

---

## Job and Workspace Isolation

To ensure deterministic builds and prevent side effects between release jobs, workspace isolation and sanitization are enforced before and after every execution.

### Sanitization Sequence
Before every job run (via pre-job hook or provisioning script):
1. **Process Cleanup:** Terminate lingering background processes that could lock binaries or hold handles:
   - `Siemens.Engineering.AddIn.Publisher.exe`
   - `TiaMcpServer.exe`
   - `TiaAgent.Bridge.exe`
   - `dotnet.exe` (stale build worker instances)
2. **Workspace Cleaning:** Execute git clean to remove untracked files and artifacts:
   ```powershell
   git clean -ffdx
   git reset --hard HEAD
   ```
3. **Temp Directory Purge:** Clear ephemeral build files in `%TEMP%\TiaAgentBuild` and `%LOCALAPPDATA%\TiaAgent\temp`.

### Ephemeral Runner Options
For maximum security, self-hosted release runners can be configured in ephemeral mode (`./run.cmd --once`), which automatically deregisters and replaces the runner container/VM after a single release job completes.

---

## Runner Provisioning and Automated Check

Automated setup and readiness verification are provided by `scripts/runner/provision-release-runner.ps1`.

### Running Verification
To verify that a Windows host meets all release runner requirements:
```powershell
./scripts/runner/provision-release-runner.ps1 -VerifyOnly
```

### Running Provisioning and Process Cleanup
To sanitize processes and prepare the runner environment:
```powershell
./scripts/runner/provision-release-runner.ps1 -SanitizeWorkspace
```

### Manual Packaging Verification Command
To verify that the runner can compile, package, and test the Add-In and CLI artifacts from a clean workspace:
```powershell
# 1. Sanitize workspace
./scripts/runner/provision-release-runner.ps1 -SanitizeWorkspace

# 2. Build, test, and package release assets
./build.ps1 all
```

---

## Maintenance and Disaster Recovery

### OS and Tool Patching
- Windows Security Updates are applied monthly during designated maintenance windows.
- TIA Portal service packs and updates must be validated in a staging environment prior to updating the release runner.
- The .NET SDK version must remain aligned with `global.json`.

### Backup and Recovery
1. **Configuration Backup:** Runner configuration scripts and system dependencies are version-controlled in the repository (`scripts/runner/`).
2. **Signing Certificate Recovery:** Code-signing certificates are backed up securely in an offsite corporate Key Vault / HSM.
3. **Host Rebuilding:** In the event of host failure or compromise:
   - De-register the compromised runner in GitHub Repository / Organization settings.
   - Provision a fresh Windows host.
   - Run `scripts/runner/provision-release-runner.ps1`.
   - Register the runner using a short-lived registration token:
     ```powershell
     .\config.cmd --url https://github.com/industrix-com-br/tia-portal-code-agent --token <REGISTRATION_TOKEN> --labels "self-hosted,Windows,x64,tia-v21,release-runner" --name "win-release-runner-01"
     ```
   - Re-install the runner service: `.\run.cmd` or `.\svc.sh install`.

---

## PR CI Isolation Guarantee

- Pull Request CI (`.github/workflows/ci.yml`) runs on GitHub-hosted `windows-latest` runners.
- Pull Request CI does NOT require Siemens TIA Portal binaries or self-hosted runners.
- Self-hosted release runners do NOT pick up untrusted PR workflows.
