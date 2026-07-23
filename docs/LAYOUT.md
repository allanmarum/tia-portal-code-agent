# Installed Filesystem Layout & Manifest Schemas

This document defines the persistent installation layout and manifest schemas used by `tia-agent` CLI independently of the Git repository checkout.

## Overview & Objectives

1. **Independent Execution:** The CLI operates against a persistent `%LOCALAPPDATA%\TiaAgent` directory.
2. **Deterministic Manifests:** System state, active versions, and installed artifacts are tracked via versioned JSON manifests.
3. **Atomic Persistence:** All manifest writes use temporary file creation followed by atomic replacement to prevent corruption during unexpected shutdowns or process interruptions.

---

## Filesystem Layout

The standard root path is `%LOCALAPPDATA%\TiaAgent`.

```text
%LOCALAPPDATA%\TiaAgent\
├── config.json              # Top-level user configuration (default runtime, overrides)
├── current.json             # Pointer to active version (CurrentManifest)
├── installations.json       # Index of installed versions and metadata (InstallationsManifest)
├── versions\                # Side-by-side version storage
│   ├── 0.2.0-beta.1\        # Specific version binaries (Bridge, AddIn, CLI)
│   └── 0.2.0-rc.1\
├── logs\                    # Log files for Bridge, AddIn, and CLI
├── runtime\                 # Active runtime supervisor state, pid files, and locks
└── cache\                   # Downloaded artifacts and temporary packages
```

### Directory Roles

- **`versions/`**: Stores unpacked version payloads side-by-side for zero-downtime activation and rollback.
- **`logs/`**: Centralized log directory across all TIA Agent components.
- **`runtime/`**: Transient runtime process state, PID files, and inter-process mutex locks.
- **`cache/`**: Temporary storage for downloaded packages before verification and installation.

---

## Manifest Schemas

### 1. `current.json` (`CurrentManifest`)

Tracks the currently active version.

```json
{
  "schemaVersion": 1,
  "activeVersion": "0.2.0-rc.1",
  "previousVersion": "0.2.0-beta.1",
  "activatedAt": "2026-07-22T21:00:00.0000000+00:00",
  "activatedBy": "tia-agent CLI"
}
```

### 2. `installations.json` (`InstallationsManifest`)

Catalog of all installed versions and component artifact checksums.

```json
{
  "schemaVersion": 1,
  "versions": {
    "0.2.0-beta.1": {
      "version": "0.2.0-beta.1",
      "installedAt": "2026-07-22T20:00:00.0000000+00:00",
      "commitSha": "af09cc0",
      "components": {
        "bridge": {
          "relativePath": "Bridge/TiaAgent.Bridge.dll",
          "sha256Hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
          "sizeBytes": 1048576
        }
      }
    }
  }
}
```

---

## Atomic Write Strategy

Manifest updates are performed via `ManifestStore.WriteAtomic<T>(filePath, data)`:

1. Data is serialized to a unique temporary file (`filePath.tmp.<guid>`).
2. The file stream is flushed and closed.
3. The temporary file replaces the target file atomically via `File.Move(tempPath, targetPath, overwrite: true)`.
4. If an exception occurs, temporary files are automatically cleaned up.

If a manifest file is missing, `ManifestStore.Read<T>` returns a default instance. If corrupted or empty, an `InvalidDataException` is raised with actionable diagnostic context.

---

## CLI Tool Package Bundling & Payload Layout

The `TiaAgent.Cli` global tool package (.nupkg) is self-contained and includes all project-owned assets necessary for standalone installation without requiring repository cloning or local compilation.

### Package Payload Layout (`tools/net8.0/any/payload/`)

Inside the NuGet package, assets are stored deterministically under `payload/`:

```text
tools/net8.0/any/payload/
├── payload-manifest.json       # Canonical manifest of bundled contents, hashes, and version alignment
├── Bridge/                     # Published Bridge binaries (TiaAgent.Bridge.dll, etc.)
├── AddIn/                      # Versioned TIA Portal Add-In artifact (TiaAgent-<version>.addin)
├── config/                     # Default configuration templates (settings.example.json, bridge.example.json, opencode.example.json)
└── notices/                    # Third-party notices and license (THIRD_PARTY_NOTICES.md, LICENSE)
```

### `payload-manifest.json` (`PayloadManifest`)

Manifest schema generated at pack time and verified during extraction:

```json
{
  "schemaVersion": 1,
  "productVersion": "0.2.0-beta.1",
  "commitSha": "64ec9ba",
  "builtAt": "2026-07-22T21:00:00.0000000+00:00",
  "compatibility": {
    "tiaPortalVersion": "V21",
    "opennessVersion": "V21",
    "targetFramework": "net8.0"
  },
  "components": {
    "bridge": {
      "relativePath": "Bridge/TiaAgent.Bridge.dll",
      "version": "0.2.0-beta.1",
      "sha256Hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "sizeBytes": 1048576
    },
    "addin": {
      "relativePath": "AddIn/TiaAgent-0.2.0.addin",
      "version": "0.2.0-beta.1",
      "sha256Hash": "a1b2c3d4...",
      "sizeBytes": 524288
    }
  },
  "files": [
    {
      "relativePath": "Bridge/TiaAgent.Bridge.dll",
      "sha256Hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "sizeBytes": 1048576
    }
  ]
}
```

### Licensing & Siemens Assembly Boundaries

- **Proprietary Siemens Binaries Excluded:** Siemens TIA Portal runtime assemblies (e.g. `Siemens.Engineering.dll`, `Siemens.Engineering.AddIn.dll`) are host-provided by Siemens TIA Portal installations and MUST NEVER be redistributed in the CLI package or stored in source control.
- **Verification Enforcement:** `PayloadValidator` and build verification targets automatically scan payload directories and reject packages if any `Siemens.*` assembly is present.
- **Licensing Compliance:** Project-owned binaries, notices, and default configs are licensed under the repository license (`LICENSE`) and third-party notices (`THIRD_PARTY_NOTICES.md`).

---

## Release Distribution Artifacts & Schemas

The release packaging process (`.\build.ps1 pack-release`) produces release metadata artifacts alongside `.addin` and `.nupkg` binaries in `artifacts/`:

- **`release-manifest.json`**: Canonical release manifest containing version, channel, commit SHA, build timestamp, compatibility matrix, component versions, and artifact hashes.
- **`SHA256SUMS`**: Standard SHA256 checksums file covering all published release artifacts.
- **`sbom.spdx.json`**: SPDX 2.3 JSON Software Bill of Materials.
- **`THIRD_PARTY_NOTICES.md`**: Open-source component third-party notices.

### `release-manifest.json` (`ReleaseManifest`)

```json
{
  "schemaVersion": 1,
  "productVersion": "0.2.0-beta.1",
  "channel": "beta",
  "commitSha": "af09cc0",
  "publishedAt": "2026-07-23T10:42:00.0000000+00:00",
  "compatibility": {
    "tiaPortalVersion": "V21",
    "opennessVersion": "V21",
    "targetFramework": "net8.0"
  },
  "components": {
    "addin": {
      "relativePath": "TiaAgent-0.2.0-beta.1.addin",
      "version": "0.2.0-beta.1",
      "sha256Hash": "a1b2c3...",
      "sizeBytes": 524288
    },
    "cli": {
      "relativePath": "TiaAgent.Cli.0.2.0-beta.1.nupkg",
      "version": "0.2.0-beta.1",
      "sha256Hash": "d4e5f6...",
      "sizeBytes": 1048576
    }
  },
  "artifacts": [
    {
      "name": "TiaAgent-0.2.0-beta.1.addin",
      "sha256Hash": "a1b2c3...",
      "sizeBytes": 524288,
      "component": "addin"
    },
    {
      "name": "release-manifest.json",
      "sha256Hash": "e3b0c4...",
      "sizeBytes": 1024,
      "component": "manifest"
    },
    {
      "name": "SHA256SUMS",
      "sha256Hash": "f8a9b0...",
      "sizeBytes": 512,
      "component": "checksums"
    },
    {
      "name": "sbom.spdx.json",
      "sha256Hash": "c1d2e3...",
      "sizeBytes": 4096,
      "component": "sbom"
    },
    {
      "name": "THIRD_PARTY_NOTICES.md",
      "sha256Hash": "b2c3d4...",
      "sizeBytes": 2048,
      "component": "notices"
    }
  ]
}
```

Validation of the release distribution can be executed via CLI:
```bash
tia-agent verify-release artifacts/
```


