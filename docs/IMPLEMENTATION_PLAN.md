# TIA Portal AI Agent Platform — Implementation Plan

**Status**: proposed  
**Date**: 2026-07-19  
**Target**: TIA Portal V21, .NET Framework 4.8, x64  
**MVP**: Read-only, simulator-first

---

## 1. Target Framework Decision Matrix

| Project | Target | Rationale |
|---|---|---|
| `TiaAgent.Contracts` | `netstandard2.0` | Compatible with both net48 and net8.0 consumers |
| `TiaAgent.Application` | `netstandard2.0` | Business logic shared across Add-In (net48) and MCP (net8.0) |
| `TiaAgent.Openness` | `net48` | Must load inside TIA Portal V21 process |
| `TiaAgent.Mcp` | `net8.0` | `ModelContextProtocol.AspNetCore` requires net8.0 |
| `TiaAgent.McpHost` | `net8.0` | ASP.NET Core host for MCP server |
| `TiaAgent.OpenCode` | `netstandard2.0` | Client library consumed by Add-In (net48) |
| `TiaAgent.Simulator` | `netstandard2.0` | Mock implementation usable by any consumer |
| `TiaAgent.AddIn` | `net48` | TIA Portal Add-In must run inside TIA process |

### Key Constraint: The net48/net8.0 Bridge

The Add-In (net48) cannot directly reference `TiaAgent.Mcp` (net8.0). The MCP server must run in a separate process. The architecture resolves this:

```
TIA Portal process (net48)          Separate process (net8.0)
┌─────────────────────┐            ┌─────────────────────────┐
│ TiaAgent.AddIn      │◄──IPC────►│ TiaAgent.McpHost        │
│ TiaAgent.Application│  Named    │ TiaAgent.Mcp            │
│ TiaAgent.Openness   │  Pipe     │ (ASP.NET Core + MCP)    │
│ TiaAgent.Contracts  │            │ TiaAgent.Application*   │
└─────────────────────┘            └─────────────────────────┘
                                    * netstandard2.0 reference
```

The `TiaAgent.McpHost` process hosts both the MCP server AND an `ITiaProjectService` implementation that communicates back to the Add-In via Named Pipes using `TiaAgent.Contracts` DTOs.

---

## 2. NuGet Packages

### TiaAgent.Contracts (netstandard2.0)
```xml
<!-- No dependencies — pure DTOs and interfaces -->
```

### TiaAgent.Application (netstandard2.0)
```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.3" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.3" />
```

### TiaAgent.Openness (net48)
```xml
<!-- Siemens assemblies: Copy Local = False, HintPath to PublicAPI -->
<Reference Include="Siemens.Engineering.AddIn.Base">
  <HintPath>C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\Siemens.Engineering.AddIn.Base.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="Siemens.Engineering.Base">
  <HintPath>C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\Siemens.Engineering.Base.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="Siemens.Engineering.Step7">
  <HintPath>C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\Siemens.Engineering.Step7.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="Siemens.Engineering.AddIn.Step7">
  <HintPath>C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\Siemens.Engineering.AddIn.Step7.dll</HintPath>
  <Private>false</Private>
</Reference>
```

### TiaAgent.Mcp (net8.0)
```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.4.1" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.3" />
```

### TiaAgent.McpHost (net8.0)
```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.4.1" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.3" />
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.3" />
```

### TiaAgent.OpenCode (netstandard2.0)
```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.3" />
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

### TiaAgent.Simulator (netstandard2.0)
```xml
<!-- No external dependencies -->
```

### TiaAgent.AddIn (net48)
```xml
<!-- Siemens assemblies: same as Openness project -->
<!-- Plus: -->
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

### Test Projects (net8.0)
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="FluentAssertions" Version="7.2.0" />
```

---

## 3. Build Strategy (No Visual Studio)

### 3.1 Solution Structure

```
TIA-Portal-Code-Agent/
├── TiaAgent.sln
├── Directory.Build.props          # Shared properties (version, TIA path)
├── Directory.Build.targets        # Shared build logic
├── global.json                   # SDK version pin
├── NuGet.Config                  # Package sources
├── src/
│   ├── TiaAgent.Contracts/
│   ├── TiaAgent.Application/
│   ├── TiaAgent.Openness/
│   ├── TiaAgent.Mcp/
│   ├── TiaAgent.McpHost/
│   ├── TiaAgent.OpenCode/
│   ├── TiaAgent.Simulator/
│   └── TiaAgent.AddIn/
├── tests/
│   ├── TiaAgent.Contracts.Tests/
│   ├── TiaAgent.Application.Tests/
│   ├── TiaAgent.Mcp.Tests/
│   └── TiaAgent.Integration.Tests/
├── agents/                       # Agent prompt files
│   ├── tia-explain.md
│   └── tia-review.md
├── config/
│   ├── appsettings.example.json
│   └── opencode.example.json
└── docs/
    ├── IMPLEMENTATION_PLAN.md
    ├── ARCHITECTURE.md
    └── ...
```

### 3.2 Build Commands

```bash
# Restore all packages
dotnet restore TiaAgent.sln

# Build all projects
dotnet build TiaAgent.sln --configuration Release

# Run all tests
dotnet test TiaAgent.sln --configuration Release

# Build specific project
dotnet build src/TiaAgent.McpHost/TiaAgent.McpHost.csproj -c Release
```

### 3.3 global.json
```json
{
  "sdk": {
    "version": "8.0.423",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

### 3.4 Directory.Build.props
```xml
<Project>
  <PropertyGroup>
    <TiaPortalVersion>V21</TiaPortalVersion>
    <TiaPublicApiDir Condition="'$(TiaPublicApiDir)' == ''">
      $(ProgramFiles)\Siemens\Automation\Portal V21\PublicAPI\V21\net48
    </TiaPublicApiDir>
    <Version>0.1.0</Version>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## 4. Phased Implementation Order

### Phase 0: Foundation (Days 1-2)
**Goal**: Solution builds, contracts defined, simulator works

#### Step 0.1: Solution Scaffold
Create solution and all project files with correct TFMs.

**Files to create:**
- `TiaAgent.sln`
- `global.json`
- `Directory.Build.props`
- `NuGet.Config`
- `src/TiaAgent.Contracts/TiaAgent.Contracts.csproj`
- `src/TiaAgent.Application/TiaAgent.Application.csproj`
- `src/TiaAgent.Openness/TiaAgent.Openness.csproj`
- `src/TiaAgent.Mcp/TiaAgent.Mcp.csproj`
- `src/TiaAgent.McpHost/TiaAgent.McpHost.csproj`
- `src/TiaAgent.OpenCode/TiaAgent.OpenCode.csproj`
- `src/TiaAgent.Simulator/TiaAgent.Simulator.csproj`
- `src/TiaAgent.AddIn/TiaAgent.AddIn.csproj`
- `tests/TiaAgent.Contracts.Tests/TiaAgent.Contracts.Tests.csproj`
- `tests/TiaAgent.Application.Tests/TiaAgent.Application.Tests.csproj`
- `tests/TiaAgent.Mcp.Tests/TiaAgent.Mcp.Tests.csproj`
- `tests/TiaAgent.Integration.Tests/TiaAgent.Integration.Tests.csproj`

**Verification:**
```bash
dotnet restore TiaAgent.sln
dotnet build TiaAgent.sln
```

#### Step 0.2: Core Contracts
Define all DTOs, interfaces, and error codes from ARCHITECTURE.md.

**Key files in TiaAgent.Contracts:**
```csharp
// Dtos/
TiaContextDto.cs           // tiaVersion, projectId, projectName, plcCount, lastModified
SelectionSnapshotDto.cs    // selectionToken, objects[], tiaSessionId, projectId
BlockSummaryDto.cs         // objectId, name, type, path, language, contentHash
BlockDto.cs                // extends summary + sourceCode, interfaceDefinition
BlockInterfaceDto.cs       // inputParams[], outputParams[], staticVars[], inOutParams[]
ObjectReference.cs         // tiaSessionId, projectId, objectId, objectType, expectedContentHash
ReferenceDto.cs            // sourceObjectId, targetObjectId, referenceType
CallHierarchyDto.cs        // rootObjectId, nodes[], edges[]
CompileResultDto.cs        // success, messages[], duration
ChangePreviewDto.cs        // changeSetId, diff, risks[], changeSetHash
ApprovedChangeRequest.cs   // changeSetId, approvalToken, content
ChangeSetDto.cs            // changeSetId, targets[], diffHash, expiresAt
ApprovalToken.cs           // token, changeSetId, diffHash, approvedBy, expiresAt, scope[]

// Errors/
TiaErrorCode.cs            // enum: TIA_NOT_CONNECTED, TIA_PROJECT_NOT_OPEN, etc.
TiaError.cs                // code, message, retryable, correlationId, details

// Requests/
ReadBlockRequest.cs
ListBlocksRequest.cs       // pageSize, cursor
GetCallHierarchyRequest.cs // maxDepth, maxNodes
FindReferencesRequest.cs
CompileRequest.cs

// Events/
TaskStartedEvent.cs
ToolCallEvent.cs
ProgressEvent.cs
TaskCompletedEvent.cs

// Abstractions/
ITiaProjectService.cs      // The canonical interface from ARCHITECTURE.md
```

**Verification:**
```bash
dotnet build src/TiaAgent.Contracts/
```

#### Step 0.3: Simulator
Implement `ITiaProjectService` with in-memory fake data.

**Key files in TiaAgent.Simulator:**
```csharp
SimulatorTiaProjectService.cs  // ITiaProjectService implementation
SimulatorData.cs               // Pre-loaded fake blocks, tags, references
SimulatorBlockFactory.cs       // Creates realistic SCL/STL/LAD content
```

**Verification:**
```bash
dotnet test tests/TiaAgent.Application.Tests/
```

---

### Phase 1: MCP Server (Days 3-5)
**Goal**: MCP server runs standalone, tools callable via HTTP

#### Step 1.1: MCP Host Console App
Standalone ASP.NET Core app that hosts the MCP server.

**Key files in TiaAgent.McpHost:**
```csharp
Program.cs                    // WebApplication setup, DI registration
McpServerOptions.cs           // Configuration (port, auth, limits)
```

**Key files in TiaAgent.Mcp:**
```csharp
// Tools/
TiaContextTools.cs            // tia_get_current_context, tia_get_selection
TiaReadTools.cs               // tia_read_block, tia_list_blocks, tia_get_block_interface
TiaReferenceTools.cs          // tia_get_call_hierarchy, tia_find_references
TiaCompileTools.cs            // tia_compile_software, tia_get_compile_messages

// Auth/
LocalAuthService.cs           // Bearer token validation
SessionManager.cs             // Session lifecycle

// Transport/
McpEndpointBuilder.cs         // Maps tools to handlers
```

**Verification:**
```bash
# Start MCP server
dotnet run --project src/TiaAgent.McpHost/

# Test with curl/MCP client
curl http://127.0.0.1:5000/mcp -H "Authorization: Bearer <token>"
```

#### Step 1.2: Tool Implementations
Each tool delegates to `ITiaProjectService` via Application layer.

**Tool → Service mapping:**
| MCP Tool | Application Method | Risk Class |
|---|---|---|
| `tia_get_current_context` | `GetCurrentContextAsync` | R0 |
| `tia_get_selection` | `GetSelectionAsync` | R0 |
| `tia_read_block` | `ReadBlockAsync` | R1 |
| `tia_list_blocks` | `ListBlocksAsync` | R1 |
| `tia_get_block_interface` | `GetBlockInterfaceAsync` | R1 |
| `tia_get_call_hierarchy` | `GetCallHierarchyAsync` | R1 |
| `tia_find_references` | `FindReferencesAsync` | R1 |

**Verification:**
```bash
dotnet test tests/TiaAgent.Mcp.Tests/
```

---

### Phase 2: Simulator Demo (Days 6-7)
**Goal**: Run simulator → MCP → exercise all read tools

#### Step 2.1: End-to-End Console Demo
```csharp
// Program.cs in TiaAgent.McpHost
var services = new ServiceCollection();
services.AddSingleton<ITiaProjectService, SimulatorTiaProjectService>();
services.AddMcpServer();
var provider = services.BuildServiceProvider();
// Start MCP server with all tools registered
```

**Verification:**
```bash
dotnet run --project src/TiaAgent.McpHost/
# In another terminal:
mcp-cli --url http://127.0.0.1:5000/mcp call tia_get_current_context
mcp-cli --url http://127.0.0.1:5000/mcp call tia_read_block --blockName "FB_Conveyor"
```

---

### Phase 3: Add-In Shell (Days 8-10)
**Goal**: Add-In loads in TIA Portal, shows context menu

#### Step 3.1: Add-In Provider
```csharp
// src/TiaAgent.AddIn/
ProjectTreeProvider.cs        // ProjectTreeAddInProvider
ExplainBlockCommand.cs        // Context menu action
AddInBootstrap.cs             // Service registration, lifecycle
```

#### Step 3.2: IPC Client (Named Pipe)
The Add-In connects to the external MCP Host process via Named Pipes.

```csharp
// src/TiaAgent.AddIn/
Ipc/
McpHostClient.cs              // Named pipe client
McpHostManager.cs             // Starts/monitors external process
```

**Verification:**
- Add-In loads in TIA Portal V21
- Context menu appears on supported objects
- Console output confirms IPC connection

---

### Phase 4: Openness Adapter (Days 11-14)
**Goal**: Real TIA Portal data flows through MCP

#### Step 4.1: ITiaProjectService Implementation
```csharp
// src/TiaAgent.Openness/
TiaProjectService.cs          // Main implementation
ObjectMapper.cs               // Siemens objects → DTOs
SessionManager.cs             // TIA session lifecycle
CapabilityDetector.cs         // V21 feature detection
```

**Verification:**
```bash
dotnet test tests/TiaAgent.Integration.Tests/
# Requires TIA Portal V21 running with a project open
```

---

### Phase 5: Explain-Block Flow (Days 15-17)
**Goal**: User clicks "Explain Block" in TIA → AI agent explains it

#### Step 5.1: Task Orchestration
```csharp
// src/TiaAgent.Application/
Tasks/
ITaskOrchestrator.cs
ExplainBlockTask.cs           // Captures context → calls MCP → returns explanation
```

#### Step 5.2: OpenCode Integration
```csharp
// src/TiaAgent.OpenCode/
IOpenCodeClient.cs
OpenCodeClient.cs             // HTTP client for agent runtime
TaskSubmission.cs
```

**Verification:**
```bash
# Full flow test:
dotnet run --project src/TiaAgent.McpHost/
# In TIA Portal: right-click block → "AI Assistant" → "Explain this block"
# → Agent calls tia_get_selection, tia_read_block
# → Returns structured explanation
```

---

### Phase 6: Polish & Testing (Days 18-20)
**Goal**: Production-ready MVP

- Error handling for all edge cases
- Cancellation support
- Timeout enforcement
- Audit logging
- Package generation with Publisher
- Documentation updates

---

## 5. Key Interface Signatures

### ITiaProjectService (Contracts)
```csharp
public interface ITiaProjectService
{
    Task<TiaContextDto> GetCurrentContextAsync(
        CancellationToken cancellationToken);

    Task<SelectionSnapshotDto> GetSelectionAsync(
        string selectionToken,
        CancellationToken cancellationToken);

    Task<BlockDto> ReadBlockAsync(
        ObjectReference reference,
        CancellationToken cancellationToken);

    Task<PagedResult<BlockSummaryDto>> ListBlocksAsync(
        ListBlocksRequest request,
        CancellationToken cancellationToken);

    Task<BlockInterfaceDto> GetBlockInterfaceAsync(
        ObjectReference reference,
        CancellationToken cancellationToken);

    Task<CallHierarchyDto> GetCallHierarchyAsync(
        GetCallHierarchyRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReferenceDto>> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken);

    Task<CompileResultDto> CompileAsync(
        CompileRequest request,
        CancellationToken cancellationToken);
}
```

### MCP Tool Attribute Pattern
```csharp
[McpServerTool]
public Task<TiaContextDto> TiaGetCurrentContext(
    CancellationToken cancellationToken)
{
    return _handler.HandleAsync(cancellationToken);
}
```

### Named Pipe IPC Protocol
```csharp
public interface ITiaIpcContract
{
    Task<TiaContextDto> GetCurrentContextAsync(
        IpcRequest request,
        CancellationToken cancellationToken);

    Task<BlockDto> ReadBlockAsync(
        ReadBlockIpcRequest request,
        CancellationToken cancellationToken);
}
```

---

## 6. Simulator Data Model

```csharp
public class SimulatorData
{
    public TiaContextDto Context { get; } = new()
    {
        TiaVersion = "V21",
        ProjectId = "sim-project-001",
        ProjectName = "SimulatorDemo",
        PlcCount = 2,
        LastModified = DateTime.UtcNow
    };

    public List<BlockSummaryDto> Blocks { get; } = new()
    {
        new() { ObjectId = "block-001", Name = "FB_Conveyor", Type = "FunctionBlock",
                Language = "SCL", Path = "PLC_1/Program blocks/FB_Conveyor" },
        new() { ObjectId = "block-002", Name = "FC_StartStop", Type = "Function",
                Language = "LAD", Path = "PLC_1/Program blocks/FC_StartStop" },
        new() { ObjectId = "block-003", Name = "DB_Parameters", Type = "DataBlock",
                Language = "SCL", Path = "PLC_1/Program blocks/DB_Parameters" },
        new() { ObjectId = "block-004", Name = "OB_Main", Type = "OrganizationBlock",
                Language = "STL", Path = "PLC_1/Program blocks/OB_Main" }
    };

    // Realistic SCL content for FB_Conveyor
    public string GetBlockSource(string blockId) => blockId switch
    {
        "block-001" => @"FUNCTION_BLOCK ""FB_Conveyor""
VAR_INPUT
    Enable : BOOL;
    Speed : INT;
END_VAR
VAR_OUTPUT
    Running : BOOL;
    CurrentSpeed : INT;
END_VAR
VAR
    _state : INT;
END_VAR

BEGIN
    IF Enable THEN
        _state := 1;
        CurrentSpeed := Speed;
        Running := TRUE;
    ELSE
        _state := 0;
        CurrentSpeed := 0;
        Running := FALSE;
    END_IF;
END_FUNCTION_BLOCK",
        _ => throw new TiaErrorException(TiaErrorCode.TIA_OBJECT_NOT_FOUND)
    };
}
```

---

## 7. Verification Checklist

After each phase, run:

```bash
# Full solution build
dotnet build TiaAgent.sln -c Release

# All tests pass
dotnet test TiaAgent.sln -c Release

# No Siemens binaries in source control
git ls-files | Select-String "Siemens.Engineering"

# Dependency graph check (no cycles)
dotnet build TiaAgent.sln -c Release /pp:graph.xml
# Inspect graph.xml for forbidden dependencies
```

---

## 8. Risk Mitigations

| Risk | Mitigation |
|---|---|
| MCP SDK incompatible with net48 | Run MCP in separate net8.0 process |
| Publisher.exe not found | Use VS Code template + manual Config.xml |
| TIA threading violations | All operations via dispatcher, never UI thread |
| Engineering object leaks | Simulator enforces local-only pattern |
| Named Pipe auth bypass | Ephemeral tokens, session binding |
| Prompt injection | Treat all TIA content as untrusted data |

---

## 9. Success Criteria

The demo is complete when:

1. `dotnet build TiaAgent.sln` succeeds
2. `dotnet test TiaAgent.sln` passes
3. `dotnet run --project src/TiaAgent.McpHost/` starts MCP server
4. Simulator provides realistic block data
5. All read-only tools return valid DTOs
6. Error codes match ARCHITECTURE.md specification
7. No Siemens binaries in source control
8. Dependency graph has no cycles
