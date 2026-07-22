# Dependency Management Policy

This document defines the dependency management, SDK pinning, package restore, and automated update policies for TIA Portal Code Agent.

## Overview & Objectives

To guarantee reproducible local and CI builds, prevent dependency drift, and maintain supply chain security:
1. **SDK Pinning:** The required .NET SDK version and roll-forward policy are declared centrally in `global.json`.
2. **Central Package Management (CPM):** All NuGet package versions are managed centrally in `Directory.Packages.props`. Individual project files (`*.csproj`) reference dependencies without specifying package versions.
3. **Locked Restore:** Package lock files are enabled via MSBuild (`RestorePackagesWithLockFile`), and locked restore mode (`RestoreLockedMode`) is enforced during CI runs.
4. **Automated Dependabot Updates:** Dependabot scans NuGet and GitHub Actions dependencies weekly, generating controlled PRs with rate limits and structured commit messages.

---

## .NET SDK Policy (`global.json`)

The workspace specifies a supported .NET SDK version and roll-forward strategy in `global.json`:

```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

- **SDK Version:** `8.0.400` (.NET 8.0 SDK baseline).
- **Roll Forward:** `latestFeature` allows building with any stable patch release within the .NET 8 feature band.
- **Prerelease:** Set to `false` to avoid unintended SDK prerelease roll-forward.

---

## Central Package Management (`Directory.Packages.props`)

NuGet dependencies across all projects are managed centrally using MSBuild Central Package Management.

### Rules

1. `Directory.Packages.props` in the repository root holds all `<PackageVersion Include="PackageName" Version="X.Y.Z" />` declarations.
2. Project files (`*.csproj`) MUST NOT include `Version="..."` on `<PackageReference>` elements.
3. Transitive dependency pinning is enabled via `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`.

### Structure of `Directory.Packages.props`

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="FluentAssertions" Version="8.10.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="PolySharp" Version="1.15.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

---

## Locked Restore & CI Enforcement

To prevent non-deterministic dependency resolution:

1. `Directory.Build.props` enables lock files globally:
   ```xml
   <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
   <RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>
   ```
2. During local restore, NuGet generates/updates `packages.lock.json` per project.
3. In CI builds (`CI=true`), NuGet runs in **locked mode**, failing the build if `packages.lock.json` is missing or out of sync.

---

## Dependabot & Update Automation

Dependabot is configured in `.github/dependabot.yml` to monitor both NuGet packages and GitHub Actions.

### Dependabot Rules

- **Frequency:** Weekly checks.
- **PR Concurrency:** Maximum 5 open PRs per ecosystem to maintain manageable review scope.
- **Commit Formatting:** Commit messages are prefixed with `deps`.
- **Target Branch:** `main`.

---

## Adding or Upgrading Dependencies

### Adding a New Dependency
1. Add `<PackageVersion Include="Package.Name" Version="X.Y.Z" />` to `Directory.Packages.props`.
2. Add `<PackageReference Include="Package.Name" />` to the relevant `.csproj` file(s).
3. Run `dotnet restore` locally to update lock files.
4. Commit both `Directory.Packages.props`, the project file(s), and updated lock files.

### Upgrading an Existing Dependency
1. Update the `Version` attribute in `Directory.Packages.props`.
2. Run `dotnet restore` to update `packages.lock.json`.
3. Run `dotnet test` to verify zero regression.
4. Commit the changes.
