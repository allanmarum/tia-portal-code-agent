# Clean-Machine End-to-End Installation Validation

This document defines the validation plan for testing the packaged product exactly as an external user would install and operate it.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Objective

Validate the packaged product on a clean machine without a repository checkout, ensuring all features work as expected for external users.

## Prerequisites

### Clean Machine Requirements

- Windows 10 or 11 x64 (clean installation or fresh user profile)
- No pre-existing TIA Portal Code Agent installation
- No repository checkout
- Internet connection for package download

### Required Software

| Component | Version | Installation |
|---|---|---|
| .NET SDK | 8.0+ | `winget install Microsoft.DotNet.SDK.8` |
| TiaMcpServer | 2.3.1+ | `dotnet tool install -g TiaMcpServer` |
| Agent Runtime | latest | `npm install -g opencode` (or mimo/claude) |

### TIA Portal Requirements

- Siemens TIA Portal V21 with Openness installed
- Membership in the `Siemens TIA Openness` Windows group
- A disposable project with at least one PLC

## Validation Scenarios

### Scenario 1: Fresh Installation

**Objective:** Verify clean installation from the packaged CLI.

**Steps:**

1. Install the CLI from NuGet:
   ```powershell
   dotnet tool install --global Industrix.TiaAgent.Cli --prerelease
   ```

2. Verify CLI is available:
   ```powershell
   tia-agent --help
   tia-agent version
   ```

3. Install the payload:
   ```powershell
   tia-agent install
   ```

4. Verify installation:
   ```powershell
   tia-agent doctor
   ```

**Expected Result:** All checks pass, payload is installed to `%LOCALAPPDATA%\TiaAgent\versions\<version>\`.

**Acceptance Criteria:**
- [ ] Fresh installation succeeds from the packaged CLI
- [ ] No source-tree paths are referenced

### Scenario 2: Environment Diagnostics

**Objective:** Verify doctor reports a healthy environment.

**Steps:**

1. Run environment diagnostics:
   ```powershell
   tia-agent doctor
   ```

2. Run verbose diagnostics:
   ```powershell
   tia-agent doctor --verbose
   ```

3. Check runtime status:
   ```powershell
   tia-agent status
   ```

**Expected Result:** All checks pass, no blocking issues detected.

**Acceptance Criteria:**
- [ ] Doctor reports a healthy environment before the functional test

### Scenario 3: Service Lifecycle

**Objective:** Verify start, status, and stop commands work correctly.

**Steps:**

1. Start services:
   ```powershell
   tia-agent start
   ```

2. Check status:
   ```powershell
   tia-agent status
   ```

3. Verify health endpoint:
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:43119/health"
   ```

4. Stop services:
   ```powershell
   tia-agent stop
   ```

**Expected Result:** Services start, report healthy status, and stop gracefully.

**Acceptance Criteria:**
- [ ] Services start and stop correctly
- [ ] Health endpoint responds

### Scenario 4: TIA Portal Integration

**Objective:** Verify the complete TIA-to-agent round trip.

**Steps:**

1. Start services:
   ```powershell
   tia-agent start
   ```

2. Open TIA Portal V21 and load a disposable project.

3. Activate the Add-In:
   - Go to **Options > Settings > Add-Ins**
   - Enable **TIA Portal Code Agent**

4. Right-click a PLC object and select an AI Assistant action.

5. Verify the response is returned to the Add-In.

6. Stop services:
   ```powershell
   tia-agent stop
   ```

**Expected Result:** The complete round trip succeeds: TIA Add-In → Bridge → runtime → MCP → response.

**Acceptance Criteria:**
- [ ] The complete TIA-to-agent round trip succeeds on a disposable project

### Scenario 5: Update and Rollback

**Objective:** Verify update and rollback commands work correctly.

**Steps:**

1. List installed versions:
   ```powershell
   tia-agent versions
   ```

2. Update to latest payload:
   ```powershell
   tia-agent update
   ```

3. Verify update:
   ```powershell
   tia-agent version
   ```

4. Rollback to previous version:
   ```powershell
   tia-agent rollback
   ```

5. Verify rollback:
   ```powershell
   tia-agent version
   ```

**Expected Result:** Update and rollback succeed, version changes correctly.

**Acceptance Criteria:**
- [ ] Update, rollback, and uninstall/reinstall are validated

### Scenario 6: Uninstall and Reinstall

**Objective:** Verify uninstall and reinstall work correctly.

**Steps:**

1. Uninstall all versions:
   ```powershell
   tia-agent uninstall --all
   ```

2. Verify uninstall:
   ```powershell
   tia-agent versions
   ```

3. Reinstall:
   ```powershell
   tia-agent install
   ```

4. Verify reinstall:
   ```powershell
   tia-agent doctor
   ```

**Expected Result:** Uninstall removes all versions, reinstall succeeds.

**Acceptance Criteria:**
- [ ] Uninstall and reinstall are validated

### Scenario 7: Runtime Adapter Validation

**Objective:** Verify all supported runtime adapters work correctly.

**Steps:**

1. Configure each runtime:
   ```powershell
   tia-agent config set runtimes.opencode.enabled true
   tia-agent config set runtimes.mimo.enabled true
   tia-agent config set runtimes.claude.enabled true
   ```

2. Validate each runtime:
   ```powershell
   tia-agent runtime doctor opencode
   tia-agent runtime doctor mimo
   tia-agent runtime doctor claude
   ```

3. Test each runtime with a simple task.

**Expected Result:** All runtime adapters pass validation and execute tasks correctly.

**Acceptance Criteria:**
- [ ] OpenCode, Mimo, and Claude Code adapters are validated

## Logs and Diagnostics

### Log Locations

| Log | Path | Contents |
|---|---|---|
| Supervisor log | `%LOCALAPPDATA%\TiaAgent\logs\supervisor.log` | Startup, shutdown, health checks, errors |
| Bridge log | `%LOCALAPPDATA%\TiaAgent\logs\bridge.log` | Task lifecycle, runtime calls, errors |
| Add-In log | `%LOCALAPPDATA%\TiaAgent\addin.log` | Action triggers, Bridge client calls, results |

### Diagnostic Commands

```powershell
# Environment health check
tia-agent doctor --verbose

# Runtime status
tia-agent status

# Version information
tia-agent version --verbose

# Installed versions
tia-agent versions

# Runtime-specific diagnostics
tia-agent runtime doctor
tia-agent runtime doctor <runtime-id>
```

## Known Limitations

- The MVP is read-only; PLC download, safety changes, and hardware/network changes are not supported.
- The project is experimental and not ready for production use.
- Breaking changes are expected while the architecture is stabilized.

## Failure Reporting

Any failures discovered during validation should be converted into reproducible follow-up findings before beta publication. Include:

1. Environment details (OS version, TIA Portal version, runtime version)
2. Steps to reproduce
3. Expected vs. actual behavior
4. Logs and diagnostic output
5. Impact assessment

## Validation Completion

After all scenarios pass:

1. Document the validation results in the release notes.
2. Confirm all acceptance criteria are met.
3. Note any known limitations or workarounds.
4. Proceed to beta publication (Issue #55).
