# Security Policy

## 1. Safety and Security Overview

TIA Portal Code Agent operates within Siemens TIA Portal industrial automation environments. Safety and project integrity are non-negotiable priorities.

For full architectural details on trust boundaries, permissions, least privilege, prompt injection defense, and approval controls, see the authoritative specification in [`docs/spec/SECURITY_MODEL.md`](docs/spec/SECURITY_MODEL.md).

### Baseline Security Controls

- **MVP Read-Only Enforcement**: The MVP engine is strictly read-only (`<TIA.ReadOnly />` permission). No write operations, PLC downloads, safety modifications, or hardware configuration changes are permitted.
- **Local Loopback Binding Only**: All HTTP and IPC service endpoints bind strictly to loopback (`127.0.0.1`). Remote or network-exposed endpoints are strictly prohibited.
- **Least-Privilege Manifest**: Add-In manifests declare only the minimal required Siemens Openness permissions. `<UnrestrictedAccess />` is prohibited.
- **Untrusted Engineering Data**: TIA Portal project content (comments, block text, tag tables, symbol names) is treated as untrusted data. Project text cannot grant permissions or alter tool policy.
- **Supply-Chain Integrity**: Siemens proprietary binaries are never committed to source control. Third-party dependencies are pinned and audited.

## 2. Supported Versions

Security updates and vulnerability fixes are provided for the following versions:

| Component | Supported Version | Notes |
| --- | --- | --- |
| TIA Portal | V21 | Target platform |
| .NET Target | .NET Framework 4.8 / .NET 8.0 | net48 (Add-In) / net8.0 (Bridge & Tools) |
| TiaAgent Release | Latest stable (0.0.x / 0.2.x) | Active release stream |

## 3. Reporting a Vulnerability

We take the security of TIA Portal Code Agent seriously. If you discover or suspect a security vulnerability, please report it responsibly:

### How to Report

- **Private Vulnerability Reporting**: Use GitHub Private Vulnerability Reporting on the repository (via **Security → Advisory → New draft advisory**).
- **Direct Email**: If private vulnerability reporting is unavailable, email `security@industrix.com.br` or contact the maintainer specified in [`.github/CODEOWNERS`](.github/CODEOWNERS).

### What to Include

- A detailed description of the vulnerability and potential impact.
- Steps to reproduce or proof-of-concept code.
- Affected components (e.g. Add-In, Bridge, Runtime Supervisor, OpenCode client).
- Any mitigation or suggested fixes if available.

### Disclosure Policy

- Please do **not** disclose security vulnerabilities publicly (such as via public GitHub issues or public forums) before maintainers have investigated and addressed the issue.
- Maintainers will acknowledge receipt of security reports within 48 hours.
- We will work with you to analyze, fix, and publish an advisory in a timely manner.
