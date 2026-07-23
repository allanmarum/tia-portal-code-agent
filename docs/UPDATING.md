# Update Guide

How to update an existing TIA Portal Code Agent installation.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Check current version

Display the currently active version and CLI product version:

```powershell
tia-agent version
```

For detailed version information including installed versions, configuration path, and environment:

```powershell
tia-agent version --verbose
```

List all installed versions with active and rollback candidates:

```powershell
tia-agent versions
```

## Update to latest payload

If you have a newer CLI package installed (via `dotnet tool update`) and want to activate its bundled payload:

```powershell
tia-agent update
```

## Update to a specific version

Update to an explicit version from a local payload directory:

```powershell
tia-agent update --version 0.2.0-beta.1 --payload-dir /path/to/payload
```

## Update the CLI tool itself

To get a newer CLI tool version from NuGet:

```powershell
# Update to latest stable
dotnet tool update --global Industrix.TiaAgent.Cli

# Update to latest prerelease
dotnet tool update --global Industrix.TiaAgent.Cli --prerelease

# Update to a specific version
dotnet tool update --global Industrix.TiaAgent.Cli --version 0.2.0
```

After updating the CLI tool, run `tia-agent update` to activate the new payload.

## Channel-aware updates

The update command respects the configured update channel. Versions that do not belong to the current channel are rejected unless `--force` is used.

Check the current channel:

```powershell
tia-agent channel
```

Change the channel:

```powershell
tia-agent channel set stable
tia-agent channel set beta
```

Channel precedence (most to least stable): `stable > rc > beta > alpha`. Downgrading the channel requires `--force`:

```powershell
tia-agent channel set rc --force
```

## Post-update verification

After any update:

```powershell
tia-agent doctor
tia-agent status
```

Verify the Add-In is deployed:

```powershell
tia-agent version --verbose
```

### TIA Portal restart

After updating, restart TIA Portal so it picks up the new Add-In artifact. The Add-In file is redeployed automatically to `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\`.

### Restart services

If services were running, stop and restart them:

```powershell
tia-agent stop
tia-agent start
```

## Skipping versions

Updating across multiple versions is supported only when the target release explicitly states that the source version is accepted. Check the target release notes before skipping versions.

## Troubleshooting updates

If an update fails:

1. Run `tia-agent doctor` to check environment health.
2. Check the active version: `tia-agent versions`.
3. If the update left the installation in a bad state, use [rollback](ROLLBACK.md) to restore the previous version.

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common error resolution.
