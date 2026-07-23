# Troubleshooting Guide

Common errors, diagnostic commands, and resolution steps for TIA Portal Code Agent.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Diagnostic commands

### Environment health check

```powershell
tia-agent doctor
```

Runs environment and setup diagnostics covering platform, layout, manifests, update channel, Siemens integration, runtimes, and MCP server. Use `--verbose` for detailed recommendations.

### Runtime status

```powershell
tia-agent status
```

Shows the current state of runtime services (Bridge, agent runtime) and health information.

### Runtime-specific diagnostics

```powershell
tia-agent runtime doctor
tia-agent runtime doctor claude
```

Runs diagnostic checks for all registered runtimes or a specific runtime, including executable availability, version policy, and MCP integration.

### Version information

```powershell
tia-agent version --verbose
```

Shows CLI product version, active version, configuration path, OS environment, and .NET framework.

### Installed versions

```powershell
tia-agent versions
```

Lists all installed versions, the active version, rollback candidate, and update channel.

## Common errors and solutions

### "Payload validation failed"

The bundled payload is missing or corrupted.

**Diagnostic:**
```powershell
tia-agent doctor
```

**Solution:**
```powershell
tia-agent install --force
```

If the problem persists, reinstall the CLI tool:
```powershell
dotnet tool uninstall --global Industrix.TiaAgent.Cli
dotnet tool install --global Industrix.TiaAgent.Cli
tia-agent install
```

### "No previous version available for rollback"

No rollback candidate exists. This happens when only one version is installed.

**Diagnostic:**
```powershell
tia-agent versions
```

**Solution:** Install a second version to enable rollback, or perform a clean reinstall of the desired version.

### "Version '<ver>' is not installed"

The requested version does not exist in the local installation registry.

**Diagnostic:**
```powershell
tia-agent versions
```

**Solution:** Install the version first:
```powershell
dotnet tool install --global Industrix.TiaAgent.Cli --version <ver>
tia-agent install
```

### "Version '<ver>' (channel: X) is not compatible with the configured update channel '<channel>'"

The target version belongs to a different channel than configured.

**Solution:** Either change the channel or use `--force`:
```powershell
tia-agent channel set <channel>
tia-agent update --version <ver>
```

Or force the update:
```powershell
tia-agent update --version <ver> --force
```

### "The local TIA Agent Bridge is not running"

The Bridge service is not started.

**Solution:**
```powershell
tia-agent start
```

If already running, check for port conflicts:
```powershell
netstat -ano | Select-String ":43119"
```

### "Unknown runtime '<id>'"

An invalid runtime identifier was specified.

**Solution:** Check available runtimes:
```powershell
tia-agent runtime list
```

Valid runtime IDs: `opencode`, `mimo`, `claude`.

### "Selected runtime '<id>' is unavailable"

The configured runtime executable is not found on PATH.

**Diagnostic:**
```powershell
tia-agent runtime doctor
```

**Solution:** Install the runtime CLI or update the executable path in `%LOCALAPPDATA%\TiaAgent\config.json`:
```powershell
tia-agent config set runtimes.<id>.executable <path>
```

### "Port 43119 already in use"

Another process is using the Bridge port.

**Diagnostic:**
```powershell
netstat -ano | Select-String ":43119"
```

**Solution:** Kill the conflicting process, or let the Runtime Supervisor allocate an alternative port from the configured range (43100-43200).

## Log locations

| Log | Path | Contents |
|---|---|---|
| Supervisor log | `%LOCALAPPDATA%\TiaAgent\logs\supervisor.log` | Startup, shutdown, health checks, errors |
| Bridge log | `%LOCALAPPDATA%\TiaAgent\logs\bridge.log` | Task lifecycle, runtime calls, errors |
| Add-In log | `%LOCALAPPDATA%\TiaAgent\addin.log` | Action triggers, Bridge client calls, results |

View logs:

```powershell
Get-Content "$env:LOCALAPPDATA\TiaAgent\logs\supervisor.log" -Tail 50
Get-Content "$env:LOCALAPPDATA\TiaAgent\logs\bridge.log" -Tail 50
Get-Content "$env:LOCALAPPDATA\TiaAgent\addin.log" -Tail 50
```

## TIA Portal integration issues

### Add-In not showing in TIA Portal

1. Ensure TIA Portal was restarted after installation.
2. Check that `.addin` files exist in `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\`.
3. Check the Add-In log: `%LOCALAPPDATA%\TiaAgent\addin.log`.
4. Run diagnostics:
   ```powershell
   tia-agent doctor
   ```

### tia-mcp fails to connect to TIA Portal

```powershell
tia-mcp doctor
```

Common causes:
- TIA Portal is not open.
- No project is loaded.
- User is not in the `Siemens TIA Openness` group.
- TIA Portal version mismatch (must be V21).

### TIA Portal V21 not found

```
TIA Portal V21 not found at 'C:\Program Files\Siemens\Automation\Portal V21'
```

Install TIA Portal V21 or set the `TiaPublicApiDir` environment variable to point to the correct installation.

## Runtime-specific issues

### Runtime not responding

```powershell
# Check Bridge health (includes runtime info)
Invoke-RestMethod -Uri "http://127.0.0.1:43119/health"
```

If unhealthy, restart services:
```powershell
tia-agent stop
tia-agent start
```

### Silent fallback is disabled

The Bridge does **not** automatically switch to another runtime when the selected runtime fails. You must explicitly configure the desired runtime:

```powershell
tia-agent runtime use <runtime-id>
```

Or set the environment variable for the current session:
```powershell
$env:TIA_AGENT_RUNTIME = "claude"
```

### OpenCode server health check failed

If using OpenCode in server mode:
1. Verify the server is installed: `opencode --version`
2. Check the port is not in use: `netstat -ano | Select-String ":43120"`
3. Check server logs: `%LOCALAPPDATA%\TiaAgent\logs\opencode.log`

## Permission issues

### User not in Siemens TIA Openness group

```powershell
whoami /groups | Select-String "Siemens TIA Openness"
```

If missing, ask your Windows administrator to add your account to the `Siemens TIA Openness` local group.

### Manifest file locked or corrupted

If a manifest file (`current.json`, `installations.json`) is corrupted:

```powershell
tia-agent doctor
```

If `doctor` reports a malformed manifest, reinstall:
```powershell
tia-agent install --force
```

## Network and connectivity issues

All TIA Agent services bind to `127.0.0.1` (loopback) only. They are not accessible from the network.

If the Bridge cannot be reached:
1. Verify it is running: `tia-agent status`
2. Check the port: `netstat -ano | Select-String ":43119"`
3. Verify no firewall is blocking local loopback traffic.

## Data and configuration

### Configuration file location

```powershell
tia-agent config path
```

### Reset configuration

```powershell
tia-agent config reset
```

### View current configuration

```powershell
tia-agent config list
```

## Getting further help

1. Run `tia-agent doctor --verbose` and review all recommendations.
2. Check the log files listed above.
3. Review the architecture documentation in [RUN.md](RUN.md) and [RUNTIME.md](RUNTIME.md).
