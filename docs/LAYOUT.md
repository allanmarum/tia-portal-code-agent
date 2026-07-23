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
  "activeVersion": "0.2.0-beta.1",
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
