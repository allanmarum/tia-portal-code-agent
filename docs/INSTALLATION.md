# Installation Guide

Step-by-step guide for installing TIA Portal Code Agent using the CLI global tool.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Prerequisites

| Component | Version | Check |
|---|---|---|
| Windows | 10/11 x64 | System information |
| Siemens TIA Portal | V21 with Openness | `C:\Program Files\Siemens\Automation\Portal V21` |
| .NET SDK | 8.0+ | `dotnet --version` |
| .NET Framework | 4.8 | Registry: `HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` (Release >= 528040) |
| TiaMcpServer | 2.3.1+ | `dotnet tool list -g` |
| Openness group | Member of `Siemens TIA Openness` | `whoami /groups` |

At least one agent runtime must also be installed:

| Runtime | Install | Check |
|---|---|---|
| Mimo CLI | `npm install -g @mimo-ai/cli` | `mimo --version` |
| OpenCode | `npm install -g opencode` | `opencode --version` |
| Claude Code CLI | `npm install -g @anthropic-ai/claude-code` | `claude --version` |

Install the MCP server if not already present:

```powershell
dotnet tool install -g TiaMcpServer
tia-mcp doctor
```

## Stable installation

Install the latest stable release:

```powershell
dotnet tool install --global Industrix.TiaAgent.Cli
```

## Prerelease installation

Install the latest prerelease (beta, RC, or alpha):

```powershell
dotnet tool install --global Industrix.TiaAgent.Cli --prerelease
```

## Specific version installation

Install a specific version:

```powershell
dotnet tool install --global Industrix.TiaAgent.Cli --version 0.2.0-beta.1
```

## Verify installation

After installation, verify the CLI is available:

```powershell
tia-agent --help
tia-agent version
```

## Post-installation steps

### 1. Install payload

The `tia-agent install` command extracts and activates the bundled payload:

```powershell
tia-agent install
```

This:
- Extracts Bridge binaries, Add-In, and configuration to `%LOCALAPPDATA%\TiaAgent\versions\<version>\`
- Deploys the `.addin` file to `%APPDATA%\Siemens\Automation\Portal V21\UserAddIns\`
- Creates default configuration at `%LOCALAPPDATA%\TiaAgent\config.json`

### 2. Restart TIA Portal

Close and reopen TIA Portal V21 so it discovers the newly deployed Add-In.

### 3. Activate the Add-In

1. Open a project in TIA Portal V21.
2. Go to **Options > Settings > Add-Ins**.
3. Enable **TIA Portal Code Agent**.
4. Confirm any permission prompts.

### 4. Start services

```powershell
tia-agent start
```

### 5. Verify setup

```powershell
tia-agent doctor
```

All checks should report `[OK]`. Warnings indicate missing optional components; failures indicate blocking issues.

## Channel-aware installation

The CLI supports four update channels. When using `--prerelease`, the latest available version from any prerelease channel is installed. To select a specific channel after installation:

```powershell
tia-agent channel set stable    # stable only (default)
tia-agent channel set rc        # release candidates + stable
tia-agent channel set beta      # beta + RC + stable
tia-agent channel set alpha     # all prereleases + stable
```

## Data and layout

All installation data is stored under `%LOCALAPPDATA%\TiaAgent\`. See [LAYOUT.md](LAYOUT.md) for the complete filesystem layout and manifest schemas.

## Uninstallation

To remove the CLI tool:

```powershell
dotnet tool uninstall --global Industrix.TiaAgent.Cli
```

To remove all installed versions and Add-In artifacts:

```powershell
tia-agent uninstall --all
```

To remove a specific version:

```powershell
tia-agent uninstall --version 0.2.0-beta.1
```

See [UPDATING.md](UPDATING.md) for update procedures and [ROLLBACK.md](ROLLBACK.md) for rollback procedures.
