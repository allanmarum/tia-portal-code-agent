# TIA Portal Code Agent

[![Status](https://img.shields.io/badge/status-active%20development-orange)](#status)
[![TIA Portal](https://img.shields.io/badge/TIA%20Portal-V21-009999)](#requirements)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)](#requirements)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](LICENSE)

A local AI-assisted engineering interface for Siemens TIA Portal. It connects contextual Add-In actions to interchangeable coding-agent runtimes and exposes supported project data through the Model Context Protocol (MCP).

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## What it does

From a supported object in TIA Portal, an engineer can invoke actions such as:

- explain PLC blocks and project objects;
- review logic and diagnostics;
- inspect references, dependencies, and signal usage;
- generate engineering documentation.

The current MVP is read-only first. PLC download, safety changes, hardware/network changes, and unattended project modifications are not supported.

## Architecture

```mermaid
flowchart LR
    TIA[TIA Portal V21] --> ADDIN[TiaAgent Add-In]
    ADDIN -->|Local HTTP| BRIDGE[TiaAgent Bridge]
    BRIDGE --> RUNTIME{Agent Runtime}
    RUNTIME --> MIMO[Mimo CLI]
    RUNTIME --> OPENCODE[OpenCode]
    RUNTIME --> CLAUDE[Claude Code CLI]
    RUNTIME --> MCP[tia-mcp]
    MCP --> OPENNESS[TIA Portal Openness]
    OPENNESS --> PROJECT[Open TIA Project]
```

- **Add-In:** captures context, submits tasks, and displays results.
- **Bridge:** manages tasks, runtime selection, cancellation, and diagnostics.
- **Agent runtime:** handles model interaction and MCP tool calls.
- **tia-mcp:** exposes supported TIA Portal Openness capabilities.

Supported runtimes: `mimo`, `opencode`, and `claude`.

## Status

Implemented and under validation:

- TIA Portal V21 Add-In;
- contextual actions;
- local Bridge API;
- Runtime Supervisor;
- Mimo, OpenCode, and Claude Code adapters;
- MCP integration through `tia-mcp`.

Breaking changes are expected while the architecture and end-to-end workflow are stabilized.

## Requirements

- Windows 10 or 11 x64;
- Siemens TIA Portal V21 with Openness installed;
- membership in the `Siemens TIA Openness` Windows group;
- .NET SDK 8 or newer;
- `tia-mcp` installed;
- at least one supported agent runtime (`opencode`, `mimo`, or `claude`).

For contributors building from source, Visual Studio 2022 and .NET Framework Developer Pack 4.8 are also required.

## Installation

Install the CLI global tool:

```powershell
# Stable release
dotnet tool install --global TiaAgent.Cli

# Prerelease (alpha, beta, RC)
dotnet tool install --global TiaAgent.Cli --prerelease
```

Then install the payload, restart TIA Portal, and start services:

```powershell
tia-agent install
tia-agent start
```

In TIA Portal V21:

1. Go to **Options > Settings > Add-Ins**.
2. Activate **TIA Portal Code Agent**.
3. Right-click a supported PLC object and choose an AI Assistant action.

For detailed instructions, see [Installation Guide](docs/INSTALLATION.md).

## Quick commands

| Command | Description |
|---|---|
| `tia-agent version` | Show current version |
| `tia-agent doctor` | Run environment diagnostics |
| `tia-agent status` | Show runtime status and health |
| `tia-agent start` | Start and monitor runtime services |
| `tia-agent stop` | Stop runtime services |
| `tia-agent update` | Update to latest payload |
| `tia-agent rollback` | Restore previous version |
| `tia-agent channel` | View or change update channel |

For full command reference, run `tia-agent --help`.

## Documentation

- [Installation Guide](docs/INSTALLATION.md) -- prerequisites, stable/prerelease/specific version install
- [Updating](docs/UPDATING.md) -- update procedures, channel-aware updates, verification
- [Rollback](docs/ROLLBACK.md) -- restore previous versions
- [Troubleshooting](docs/TROUBLESHOOTING.md) -- common errors, diagnostics, logs
- [Running End-to-End](docs/RUN.md) -- full setup and runtime configuration
- [Runtime Configuration](docs/RUNTIME.md) -- runtime selection, modes, and configuration
- [Versioning Policy](docs/VERSIONING.md) -- semantic versioning and release channels
- [Installed Layout](docs/LAYOUT.md) -- filesystem layout and manifest schemas
- [Release Policy](docs/RELEASING.md) -- release process and support policy
- [Compatibility Policy](docs/COMPATIBILITY.md) -- TIA Portal compatibility matrix
- [Security Model](docs/spec/SECURITY_MODEL.md) -- trust boundaries, permissions, and safety

## Development

```powershell
.\build.ps1 build
.\build.ps1 test
.\build.ps1 pack
.\build.ps1 all
```

Contributors should read [AGENTS.md](AGENTS.md) and the specifications under [`docs/spec/`](docs/spec/).

## Safety

- Keep Bridge and MCP services bound to loopback.
- Do not log credentials, tokens, or unnecessary project source.
- Treat project content as untrusted data.
- Do not enable project writes without preview, concurrency checks, explicit approval, validation, and audit logging.

## Disclaimer

This is an independent project and is not affiliated with, endorsed by, or supported by Siemens, Anthropic, Mimo, OpenCode, or the maintainers of `tia-mcp`.

Siemens, SIMATIC, TIA Portal, and related product names are trademarks of their respective owners.

## Third-party assets

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for third-party
asset attribution and licensing information.

## License

Licensed under the [Apache License 2.0](LICENSE).
