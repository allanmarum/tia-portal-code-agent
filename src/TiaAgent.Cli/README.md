# TIA Portal Code Agent

[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](https://github.com/industrix-com-br/tia-portal-code-agent/blob/main/LICENSE)

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

## Requirements

- Windows 10 or 11 x64;
- Siemens TIA Portal V21 with Openness installed;
- membership in the `Siemens TIA Openness` Windows group;
- .NET SDK 8 or newer;
- `tia-mcp` installed;
- at least one supported agent runtime (`opencode`, `mimo`, or `claude`).

## Documentation

- [GitHub Repository](https://github.com/industrix-com-br/tia-portal-code-agent)
- [Installation Guide](https://github.com/industrix-com-br/tia-portal-code-agent/blob/main/docs/INSTALLATION.md)
- [Troubleshooting](https://github.com/industrix-com-br/tia-portal-code-agent/blob/main/docs/TROUBLESHOOTING.md)
- [Security Model](https://github.com/industrix-com-br/tia-portal-code-agent/blob/main/docs/spec/SECURITY_MODEL.md)

## License

Licensed under the [Apache License 2.0](https://github.com/industrix-com-br/tia-portal-code-agent/blob/main/LICENSE).
