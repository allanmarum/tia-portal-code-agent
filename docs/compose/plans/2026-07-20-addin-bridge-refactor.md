# TIA Portal Add-In → Bridge Architecture Refactor

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all modern .NET dependencies from the TIA Portal Add-In process by introducing a Bridge executable that handles OpenCode orchestration, selection capture, and session management.

**Architecture:** The Add-In becomes a thin HMI that captures TIA object snapshots and sends them to a local Bridge executable over HTTP. The Bridge (net8.0) handles all OpenCode orchestration, session management, and MCP integration. Contracts (netstandard2.0) define shared DTOs with zero modern dependencies.

**Tech Stack:** .NET Framework 4.8 (Add-In), .NET 8.0 (Bridge), netstandard2.0 (Contracts), xunit/FluentAssertions (tests)

## Global Constraints

- TIA Portal V21, .NET Framework 4.8, x64 only
- Add-In must NOT reference: TiaAgent.Application, TiaAgent.OpenCode, Microsoft.Extensions.*, System.Text.Json, Microsoft.Bcl.AsyncInterfaces
- Contracts must remain dependency-light (no Siemens, no OpenCode, no MCP)
- Bridge binds only to 127.0.0.1
- All operations need CancellationToken
- Every task needs a correlationId
- Engineering objects are local-scope only — never store IEngineeringObject across threads
- OpenCode launched via MCP through stdio, not by Bridge directly
- Keep solution functional at every stage

---

## Task 1: Remove Modern Dependencies from TiaAgent.Contracts

**Covers:** Phase 2 (Bridge contracts foundation)
**Files:**
- Modify: `src/TiaAgent.Contracts/TiaAgent.Contracts.csproj`

**Interfaces:**
- Produces: Clean TiaAgent.Contracts with no Microsoft.Bcl.AsyncInterfaces dependency

- [ ] **Step 1: Remove Microsoft.Bcl.AsyncInterfaces from Contracts**

Edit `src/TiaAgent.Contracts/TiaAgent.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <RootNamespace>TiaAgent.Contracts</RootNamespace>
    <Description>Immutable DTOs, interfaces, and error contracts for TIA Agent</Description>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Fix IOpenCodeClient.cs for netstandard2.0 without AsyncInterfaces**

Replace `IAsyncEnumerable` with `Task`-based polling pattern. Edit `src/TiaAgent.Contracts/Abstractions/IOpenCodeClient.cs`:

```csharp
namespace TiaAgent.Contracts.Abstractions;

public interface IOpenCodeClient
{
    Task<OpenCodeSessionDto> CreateSessionAsync(CreateOpenCodeSessionRequest request, CancellationToken cancellationToken);
    Task<OpenCodeTaskDto> StartTaskAsync(StartOpenCodeTaskRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<OpenCodeEventDto>> GetTaskEventsAsync(string taskId, CancellationToken cancellationToken);
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}

public class CreateOpenCodeSessionRequest
{
    public required string CorrelationId { get; init; }
    public required string TiaSessionId { get; init; }
    public required string ProjectId { get; init; }
    public string? DefaultAgent { get; init; }
}

public class StartOpenCodeTaskRequest
{
    public required string SessionId { get; init; }
    public required string CorrelationId { get; init; }
    public required string AgentId { get; init; }
    public required string Message { get; init; }
    public string? SelectionToken { get; init; }
}

public class OpenCodeSessionDto
{
    public required string SessionId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public class OpenCodeTaskDto
{
    public required string TaskId { get; init; }
    public required string SessionId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public class OpenCodeEventDto
{
    public required string EventType { get; init; }
    public required string TaskId { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
```

- [ ] **Step 3: Fix MockOpenCodeClient for new interface**

Edit `src/TiaAgent.OpenCode/Client/MockOpenCodeClient.cs` to use `Task<IReadOnlyList<OpenCodeEventDto>>` instead of `IAsyncEnumerable`.

- [ ] **Step 4: Fix OpenCodeHttpClient for new interface**

Edit `src/TiaAgent.OpenCode/Client/OpenCodeHttpClient.cs` — change `WatchTaskAsync` to `GetTaskEventsAsync` returning `Task<IReadOnlyList<OpenCodeEventDto>>` (polling-based).

- [ ] **Step 5: Fix OpenCodeOrchestrator for new interface**

Edit `src/TiaAgent.Application/OpenCode/OpenCodeOrchestrator.cs` — replace `await foreach` with polling loop using `GetTaskEventsAsync`.

- [ ] **Step 6: Build and verify no Microsoft.Bcl.AsyncInterfaces**

Run: `dotnet build TiaAgent.sln --configuration Release`
Verify: No references to Microsoft.Bcl.AsyncInterfaces in Contracts output.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: remove Microsoft.Bcl.AsyncInterfaces from TiaAgent.Contracts"
```

---

## Task 2: Remove Modern Dependencies from TiaAgent.Application

**Covers:** Phase 1 (dependency cleanup), Phase 2 (foundation)
**Files:**
- Modify: `src/TiaAgent.Application/TiaAgent.Application.csproj`

**Interfaces:**
- Produces: TiaAgent.Application without Microsoft.Extensions.* dependencies

- [ ] **Step 1: Remove Microsoft.Extensions.Logging.Abstractions**

Edit `src/TiaAgent.Application/TiaAgent.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <RootNamespace>TiaAgent.Application</RootNamespace>
    <Description>Business logic, abstractions, and application services</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TiaAgent.Contracts\TiaAgent.Contracts.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Fix OpenCodeOrchestrator to use plain interface instead of ILogger**

Replace `Microsoft.Extensions.Logging.ILogger<T>` with a simple interface. Create `src/TiaAgent.Contracts/Abstractions/ILogger.cs`:

```csharp
namespace TiaAgent.Contracts.Abstractions;

public interface ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogDebug(string message);
}

public interface ILogger<T> : ILogger { }
```

- [ ] **Step 3: Fix CorrelationContext to remove using**

Remove `using System.Threading;` if needed (it's available in netstandard2.0).

- [ ] **Step 4: Build and verify no Microsoft.Extensions references**

Run: `dotnet build TiaAgent.sln --configuration Release`
Verify: No references to Microsoft.Extensions.Logging.Abstractions or Microsoft.Extensions.DependencyInjection.Abstractions.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: remove Microsoft.Extensions.Logging from TiaAgent.Application"
```

---

## Task 3: Create Bridge Contracts in TiaAgent.Contracts

**Covers:** Phase 2 (Bridge contracts)
**Files:**
- Create: `src/TiaAgent.Contracts/Bridge/BridgeTaskRequest.cs`
- Create: `src/TiaAgent.Contracts/Bridge/TiaInstanceSnapshot.cs`
- Create: `src/TiaAgent.Contracts/Bridge/ProjectSnapshot.cs`
- Create: `src/TiaAgent.Contracts/Bridge/SelectionSnapshot.cs`
- Create: `src/TiaAgent.Contracts/Bridge/BridgeTaskAccepted.cs`
- Create: `src/TiaAgent.Contracts/Bridge/BridgeTaskStatus.cs`
- Create: `src/TiaAgent.Contracts/Bridge/BridgeError.cs`
- Create: `src/TiaAgent.Contracts/Bridge/BridgeHealthResponse.cs`
- Create: `src/TiaAgent.Contracts/Bridge/TaskStatus.cs`
- Create: `src/TiaAgent.Contracts/Bridge/BridgeAction.cs`

**Interfaces:**
- Produces: All Bridge DTOs shared between Add-In and Bridge

- [ ] **Step 1: Create Bridge DTOs**

Create `src/TiaAgent.Contracts/Bridge/BridgeTaskRequest.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskRequest
{
    public string ContractVersion { get; init; } = "1.0";
    public string CorrelationId { get; init; } = null!;
    public string Action { get; init; } = null!;
    public string AgentId { get; init; } = null!;
    public TiaInstanceSnapshot TiaInstance { get; init; } = null!;
    public ProjectSnapshot Project { get; init; } = null!;
    public SelectionSnapshot Selection { get; init; } = null!;
    public string UserMessage { get; init; } = null!;
}
```

Create `src/TiaAgent.Contracts/Bridge/TiaInstanceSnapshot.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class TiaInstanceSnapshot
{
    public int ProcessId { get; init; }
    public string SessionId { get; init; } = null!;
    public string Version { get; init; } = null!;
}
```

Create `src/TiaAgent.Contracts/Bridge/ProjectSnapshot.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class ProjectSnapshot
{
    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Path { get; init; } = null!;
}
```

Create `src/TiaAgent.Contracts/Bridge/SelectionSnapshot.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class SelectionSnapshot
{
    public string Name { get; init; } = null!;
    public string ObjectType { get; init; } = null!;
    public string RuntimeType { get; init; } = null!;
    public string PlcName { get; init; } = null!;
    public string TiaPath { get; init; } = null!;
    public string Language { get; init; } = null!;
}
```

Create `src/TiaAgent.Contracts/Bridge/BridgeTaskAccepted.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskAccepted
{
    public string TaskId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string CorrelationId { get; init; } = null!;
}
```

Create `src/TiaAgent.Contracts/Bridge/BridgeTaskStatus.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeTaskStatus
{
    public string TaskId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string Stage { get; init; } = null!;
    public string Message { get; init; } = null!;
    public string Response { get; init; } = null!;
    public BridgeError Error { get; init; }
}
```

Create `src/TiaAgent.Contracts/Bridge/BridgeError.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeError
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
    public bool Retryable { get; init; }
}
```

Create `src/TiaAgent.Contracts/Bridge/BridgeHealthResponse.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public sealed class BridgeHealthResponse
{
    public string Status { get; init; } = null!;
    public string BridgeVersion { get; init; } = null!;
    public bool OpenCodeAvailable { get; init; }
    public string OpenCodeVersion { get; init; } = null!;
    public bool McpConfigured { get; init; }
}
```

Create `src/TiaAgent.Contracts/Bridge/TaskStatus.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public static class BridgeTaskStatusValues
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string WaitingForApproval = "waiting_for_approval";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
```

Create `src/TiaAgent.Contracts/Bridge/BridgeAction.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public static class BridgeActions
{
    public const string Explain = "explain";
    public const string Review = "review";
    public const string Propose = "propose";
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build src/TiaAgent.Contracts/TiaAgent.Contracts.csproj --configuration Release`

- [ ] **Step 3: Commit**

```bash
git add src/TiaAgent.Contracts/Bridge/
git commit -m "feat: add Bridge contract DTOs to TiaAgent.Contracts"
```

---

## Task 4: Create IAgentBridgeClient Interface

**Covers:** Phase 4 (Add-In Bridge client interface)
**Files:**
- Create: `src/TiaAgent.Contracts/Bridge/IAgentBridgeClient.cs`
- Create: `src/TiaAgent.Contracts/Bridge/BridgeConfigurationException.cs`

**Interfaces:**
- Produces: IAgentBridgeClient consumed by Add-In

- [ ] **Step 1: Create IAgentBridgeClient**

Create `src/TiaAgent.Contracts/Bridge/IAgentBridgeClient.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public interface IAgentBridgeClient
{
    Task<BridgeHealthResponse> CheckHealthAsync(CancellationToken cancellationToken);
    Task<BridgeTaskAccepted> StartTaskAsync(BridgeTaskRequest request, CancellationToken cancellationToken);
    Task<BridgeTaskStatus> GetTaskAsync(string taskId, CancellationToken cancellationToken);
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken);
}
```

Create `src/TiaAgent.Contracts/Bridge/BridgeConfigurationException.cs`:
```csharp
namespace TiaAgent.Contracts.Bridge;

public class BridgeConfigurationException : Exception
{
    public BridgeConfigurationException(string message) : base(message) { }
    public BridgeConfigurationException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build src/TiaAgent.Contracts/TiaAgent.Contracts.csproj --configuration Release`

- [ ] **Step 3: Commit**

```bash
git add src/TiaAgent.Contracts/Bridge/
git commit -m "feat: add IAgentBridgeClient interface"
```

---

## Task 5: Remove Add-In References to Application and OpenCode

**Covers:** Phase 1 (dependency cleanup)
**Files:**
- Modify: `src/TiaAgent.AddIn/TiaAgent.AddIn.csproj`
- Create: `src/TiaAgent.AddIn/Bridge/BridgeClientConfig.cs`
- Create: `src/TiaAgent.AddIn/Bridge/AgentBridgeClient.cs`
- Modify: `src/TiaAgent.AddIn/AddInServices.cs`
- Modify: `src/TiaAgent.AddIn/Providers/ProjectTreeProvider.cs`
- Modify: `src/TiaAgent.AddIn/Ui/AssistantPanelFactory.cs`
- Modify: `src/TiaAgent.AddIn/Config.xml`

**Interfaces:**
- Consumes: IAgentBridgeClient, BridgeTaskRequest, BridgeTaskStatus from TiaAgent.Contracts.Bridge
- Produces: Add-In with only TiaAgent.Contracts dependency

- [ ] **Step 1: Remove project references from Add-In csproj**

Edit `src/TiaAgent.AddIn/TiaAgent.AddIn.csproj` — remove TiaAgent.Application and TiaAgent.OpenCode references:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>TiaAgent.AddIn</RootNamespace>
    <Description>TIA Portal V21 Add-In — UI, commands, selection capture, Bridge client</Description>
    <PlatformTarget>AnyCPU</PlatformTarget>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TiaAgent.Contracts\TiaAgent.Contracts.csproj" />
  </ItemGroup>

  <!-- Define SIEMENS only when TIA Portal assemblies are available -->
  <PropertyGroup Condition="'$(SiemensAssembliesExist)' == 'true'">
    <DefineConstants>$(DefineConstants);SIEMENS</DefineConstants>
  </PropertyGroup>

  <ItemGroup Condition="'$(SiemensAssembliesExist)' == 'true'">
    <Reference Include="System.Net.Http" />
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="WindowsBase" />
    <Reference Include="System.Xaml" />
    <Reference Include="System.Runtime.Serialization" />
    <Reference Include="Siemens.Engineering.Base">
      <HintPath>$(TiaPublicApiDir)\Siemens.Engineering.Base.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Siemens.Engineering.AddIn.Base">
      <HintPath>$(TiaAddInApiDir)\Siemens.Engineering.AddIn.Base.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Siemens.Engineering.AddIn.Step7">
      <HintPath>$(TiaAddInApiDir)\Siemens.Engineering.AddIn.Step7.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup Condition="'$(SiemensAssembliesExist)' != 'true'">
    <Reference Include="System.Net.Http" />
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="WindowsBase" />
    <Reference Include="System.Xaml" />
    <Reference Include="System.Runtime.Serialization" />
  </ItemGroup>

  <Import Project="PackageAddIn.targets" />
</Project>
```

- [ ] **Step 2: Create Bridge client config**

Create `src/TiaAgent.AddIn/Bridge/BridgeClientConfig.cs`:
```csharp
using System;
using System.IO;

namespace TiaAgent.AddIn.Bridge;

public sealed class BridgeClientConfig
{
    public string BridgeBaseUrl { get; set; } = "http://127.0.0.1:43119";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int PollingIntervalMilliseconds { get; set; } = 500;
    public int TaskTimeoutSeconds { get; set; } = 300;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "addin.json");

    public static BridgeClientConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFile))
            {
                var defaultConfig = new BridgeClientConfig();
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigFile);
            return Deserialize(json);
        }
        catch
        {
            return new BridgeClientConfig();
        }
    }

    public static void Save(BridgeClientConfig config)
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            var json = Serialize(config);
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
            // Best effort
        }
    }

    private static string Serialize(BridgeClientConfig config)
    {
        return "{\"bridgeBaseUrl\":\"" + EscapeJson(config.BridgeBaseUrl) + "\","
             + "\"requestTimeoutSeconds\":" + config.RequestTimeoutSeconds + ","
             + "\"pollingIntervalMilliseconds\":" + config.PollingIntervalMilliseconds + ","
             + "\"taskTimeoutSeconds\":" + config.TaskTimeoutSeconds + "}";
    }

    private static BridgeClientConfig Deserialize(string json)
    {
        var config = new BridgeClientConfig();
        // Minimal JSON parsing - extract key values
        config.BridgeBaseUrl = ExtractString(json, "bridgeBaseUrl") ?? config.BridgeBaseUrl;
        config.RequestTimeoutSeconds = ExtractInt(json, "requestTimeoutSeconds") ?? config.RequestTimeoutSeconds;
        config.PollingIntervalMilliseconds = ExtractInt(json, "pollingIntervalMilliseconds") ?? config.PollingIntervalMilliseconds;
        config.TaskTimeoutSeconds = ExtractInt(json, "taskTimeoutSeconds") ?? config.TaskTimeoutSeconds;
        return config;
    }

    private static string? ExtractString(string json, string key)
    {
        var searchKey = "\"" + key + "\":\"";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private static int? ExtractInt(string json, string key)
    {
        var searchKey = "\"" + key + "\":";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = start;
        while (end < json.Length && char.IsDigit(json[end])) end++;
        if (end == start) return null;
        if (int.TryParse(json.Substring(start, end - start), out var val))
            return val;
        return null;
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
```

- [ ] **Step 3: Create AgentBridgeClient**

Create `src/TiaAgent.AddIn/Bridge/AgentBridgeClient.cs`:
```csharp
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Bridge;

public sealed class AgentBridgeClient : IAgentBridgeClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly BridgeClientConfig _config;

    public AgentBridgeClient(BridgeClientConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.BridgeBaseUrl),
            Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
        };
    }

    public async Task<BridgeHealthResponse> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return DeserializeHealthResponse(json);
        }
        catch (HttpRequestException ex)
        {
            AddInLogger.Warn($"Bridge health check failed: {ex.Message}");
            return new BridgeHealthResponse
            {
                Status = "unreachable",
                BridgeVersion = "unknown",
                OpenCodeAvailable = false,
                OpenCodeVersion = "unknown",
                McpConfigured = false
            };
        }
    }

    public async Task<BridgeTaskAccepted> StartTaskAsync(BridgeTaskRequest request, CancellationToken cancellationToken)
    {
        var json = SerializeTaskRequest(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/v1/tasks", content, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = DeserializeBridgeError(responseJson);
            throw new BridgeTaskException(error.Code, error.Message);
        }

        return DeserializeTaskAccepted(responseJson);
    }

    public async Task<BridgeTaskStatus> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/v1/tasks/{taskId}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync();
        return DeserializeTaskStatus(json);
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _httpClient.PostAsync($"/v1/tasks/{taskId}/cancel", null, cancellationToken);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private string SerializeTaskRequest(BridgeTaskRequest request)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        AppendJsonProperty(sb, "contractVersion", request.ContractVersion);
        sb.Append(',');
        AppendJsonProperty(sb, "correlationId", request.CorrelationId);
        sb.Append(',');
        AppendJsonProperty(sb, "action", request.Action);
        sb.Append(',');
        AppendJsonProperty(sb, "agentId", request.AgentId);
        sb.Append(',');
        AppendJsonProperty(sb, "userMessage", request.UserMessage);
        sb.Append(',');
        sb.Append("\"tiaInstance\":{");
        AppendJsonProperty(sb, "processId", request.TiaInstance.ProcessId.ToString());
        sb.Append(',');
        AppendJsonProperty(sb, "sessionId", request.TiaInstance.SessionId);
        sb.Append(',');
        AppendJsonProperty(sb, "version", request.TiaInstance.Version);
        sb.Append("},");
        sb.Append("\"project\":{");
        AppendJsonProperty(sb, "id", request.Project.Id);
        sb.Append(',');
        AppendJsonProperty(sb, "name", request.Project.Name);
        sb.Append(',');
        AppendJsonProperty(sb, "path", request.Project.Path);
        sb.Append("},");
        sb.Append("\"selection\":{");
        AppendJsonProperty(sb, "name", request.Selection.Name);
        sb.Append(',');
        AppendJsonProperty(sb, "objectType", request.Selection.ObjectType);
        sb.Append(',');
        AppendJsonProperty(sb, "runtimeType", request.Selection.RuntimeType);
        if (!string.IsNullOrEmpty(request.Selection.PlcName))
        {
            sb.Append(',');
            AppendJsonProperty(sb, "plcName", request.Selection.PlcName);
        }
        if (!string.IsNullOrEmpty(request.Selection.TiaPath))
        {
            sb.Append(',');
            AppendJsonProperty(sb, "tiaPath", request.Selection.TiaPath);
        }
        if (!string.IsNullOrEmpty(request.Selection.Language))
        {
            sb.Append(',');
            AppendJsonProperty(sb, "language", request.Selection.Language);
        }
        sb.Append("}}");
        return sb.ToString();
    }

    private void AppendJsonProperty(StringBuilder sb, string key, string value)
    {
        sb.Append('"');
        sb.Append(key);
        sb.Append("\":\"");
        sb.Append(EscapeJson(value ?? ""));
        sb.Append('"');
    }

    private void AppendJsonProperty(StringBuilder sb, string key, int value)
    {
        sb.Append('"');
        sb.Append(key);
        sb.Append("\":");
        sb.Append(value);
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private BridgeHealthResponse DeserializeHealthResponse(string json)
    {
        return new BridgeHealthResponse
        {
            Status = ExtractString(json, "status") ?? "unknown",
            BridgeVersion = ExtractString(json, "bridgeVersion") ?? "unknown",
            OpenCodeAvailable = ExtractBool(json, "openCodeAvailable"),
            OpenCodeVersion = ExtractString(json, "openCodeVersion") ?? "unknown",
            McpConfigured = ExtractBool(json, "mcpConfigured")
        };
    }

    private BridgeTaskAccepted DeserializeTaskAccepted(string json)
    {
        return new BridgeTaskAccepted
        {
            TaskId = ExtractString(json, "taskId") ?? "",
            Status = ExtractString(json, "status") ?? "",
            CorrelationId = ExtractString(json, "correlationId") ?? ""
        };
    }

    private BridgeTaskStatus DeserializeTaskStatus(string json)
    {
        return new BridgeTaskStatus
        {
            TaskId = ExtractString(json, "taskId") ?? "",
            Status = ExtractString(json, "status") ?? "",
            Stage = ExtractString(json, "stage") ?? "",
            Message = ExtractString(json, "message") ?? "",
            Response = ExtractString(json, "response") ?? "",
            Error = new BridgeError
            {
                Code = ExtractString(json, "errorCode") ?? "",
                Message = ExtractString(json, "errorMessage") ?? "",
                Retryable = ExtractBool(json, "retryable")
            }
        };
    }

    private BridgeError DeserializeBridgeError(string json)
    {
        return new BridgeError
        {
            Code = ExtractString(json, "code") ?? "INTERNAL_ERROR",
            Message = ExtractString(json, "message") ?? "Unknown error",
            Retryable = ExtractBool(json, "retryable")
        };
    }

    private string? ExtractString(string json, string key)
    {
        var searchKey = "\"" + key + "\":\"";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private bool ExtractBool(string json, string key)
    {
        var searchKey = "\"" + key + "\":";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return false;
        start += searchKey.Length;
        return json.Substring(start, Math.Min(4, json.Length - start)).StartsWith("true", StringComparison.Ordinal);
    }
}

public class BridgeTaskException : Exception
{
    public string ErrorCode { get; }

    public BridgeTaskException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

- [ ] **Step 4: Refactor AddInServices to remove orchestrator**

Replace `src/TiaAgent.AddIn/AddInServices.cs` entirely:
```csharp
#if SIEMENS
using System;
using TiaAgent.AddIn.Bridge;
using TiaAgent.AddIn.Diagnostics;

namespace TiaAgent.AddIn;

public static class AddInServices
{
    private static IAgentBridgeClient? _bridgeClient;
    private static BridgeClientConfig? _config;
    private static readonly object _lock = new();

    static AddInServices()
    {
        AddInLogger.Startup();
        AddInLogger.Info("AddInServices static constructor called");
    }

    public static IAgentBridgeClient BridgeClient
    {
        get
        {
            if (_bridgeClient == null)
            {
                lock (_lock)
                {
                    _bridgeClient ??= CreateBridgeClient();
                }
            }
            return _bridgeClient;
        }
    }

    public static BridgeClientConfig Config
    {
        get
        {
            if (_config == null)
            {
                lock (_lock)
                {
                    _config ??= BridgeClientConfig.Load();
                }
            }
            return _config;
        }
    }

    public static void SetBridgeClient(IAgentBridgeClient client)
    {
        lock (_lock)
        {
            _bridgeClient = client;
        }
    }

    private static IAgentBridgeClient CreateBridgeClient()
    {
        AddInLogger.Info("Creating Bridge client");
        try
        {
            var config = BridgeClientConfig.Load();
            _config = config;
            AddInLogger.Info($"Bridge client created: {config.BridgeBaseUrl}");
            return new AgentBridgeClient(config);
        }
        catch (Exception ex)
        {
            AddInLogger.Error("Failed to create Bridge client", ex);
            throw;
        }
    }
}
#endif
```

- [ ] **Step 5: Refactor ProjectTreeProvider to use Bridge**

Replace `src/TiaAgent.AddIn/Providers/ProjectTreeProvider.cs` — the HandleAction and ExecuteViaOrchestratorAsync methods should now use Bridge:

```csharp
#if SIEMENS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
using TiaAgent.AddIn.Bridge;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.AddIn.Ui;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Providers;

public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    private readonly TiaPortal _tiaPortal;

    public ProjectTreeProvider(TiaPortal tiaPortal)
    {
        _tiaPortal = tiaPortal;
        AddInLogger.Info("ProjectTreeProvider created");
    }

    protected override System.Collections.Generic.IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        AddInLogger.Info("GetContextMenuAddIns called");
        yield return new TiaAgentContextMenu(_tiaPortal);
        yield return new TiaAgentTestContextMenu(_tiaPortal);
    }
}

public sealed class TiaAgentContextMenu : ContextMenuAddIn
{
    private readonly TiaPortal _tiaPortal;

    public TiaAgentContextMenu(TiaPortal tiaPortal) : base("AI Assistant")
    {
        _tiaPortal = tiaPortal;
    }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        var aiSubmenu = addInRoot.Items.AddSubmenu("AI Assistant");

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Explain selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("explain", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Review selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("review", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Propose change",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("propose", selection));
    }

    private void HandleAction(string action, MenuSelectionProvider<IEngineeringObject> selection)
    {
        try
        {
            AddInLogger.Info($"Action '{action}' triggered");
            var objects = selection.GetSelection();
            var enumerator = objects.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                MessageBox.Show("No object selected.", "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedObj = enumerator.Current;

            // Capture selection snapshot while still on UI thread
            var snapshot = SelectionSnapshotFactory.Create(selectedObj, _tiaPortal);
            var correlationId = $"tia-{Guid.NewGuid():N}";

            AddInLogger.Info($"Action '{action}' on {snapshot.Name} ({snapshot.ObjectType}), correlationId={correlationId}");

            // Fire-and-forget: run Bridge communication on background thread
            Task.Run(() => ExecuteViaBridgeAsync(action, snapshot, correlationId));
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Action '{action}' failed", ex);
            MessageBox.Show("Error: " + ex.Message, "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteViaBridgeAsync(string action, SelectionSnapshot snapshot, string correlationId)
    {
        try
        {
            var agentId = action switch
            {
                "explain" => "tia-explain",
                "review" => "tia-review",
                "propose" => "tia-change",
                _ => "tia-explain"
            };

            var request = new BridgeTaskRequest
            {
                CorrelationId = correlationId,
                Action = action,
                AgentId = agentId,
                TiaInstance = new TiaInstanceSnapshot
                {
                    ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    SessionId = "addin-session",
                    Version = "V21"
                },
                Project = new ProjectSnapshot
                {
                    Id = "current",
                    Name = "Current Project",
                    Path = ""
                },
                Selection = snapshot,
                UserMessage = $"The user selected object \"{snapshot.Name}\" of type \"{snapshot.ObjectType}\" in TIA Portal. Please {action} this object."
            };

            AddInLogger.Info($"Submitting task to Bridge: {action} on {snapshot.Name} (correlationId={correlationId})");

            var accepted = await AddInServices.BridgeClient.StartTaskAsync(request, CancellationToken.None);

            AddInLogger.Info($"Task accepted: {accepted.TaskId} (status={accepted.Status})");

            // Poll for completion
            var config = AddInServices.Config;
            var timeout = TimeSpan.FromSeconds(config.TaskTimeoutSeconds);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(config.PollingIntervalMilliseconds);
                var status = await AddInServices.BridgeClient.GetTaskAsync(accepted.TaskId, CancellationToken.None);

                AddInLogger.Info($"Task {accepted.TaskId}: status={status.Status}, stage={status.Stage}");

                if (status.Status == BridgeTaskStatusValues.Completed)
                {
                    AddInLogger.Info($"Task completed in {(DateTime.UtcNow - startTime).TotalSeconds:F1}s");
                    AssistantPanelFactory.ShowResult(action, status.Response ?? "No response received.");
                    return;
                }

                if (status.Status == BridgeTaskStatusValues.Failed)
                {
                    AddInLogger.Warn($"Task failed: {status.Error?.Message}");
                    AssistantPanelFactory.ShowError(status.Error?.Message ?? "Task failed");
                    return;
                }

                if (status.Status == BridgeTaskStatusValues.Cancelled)
                {
                    AssistantPanelFactory.ShowWarning("Task was cancelled.");
                    return;
                }
            }

            // Timeout
            AddInLogger.Warn($"Task timed out after {timeout.TotalSeconds}s");
            AssistantPanelFactory.ShowError("The operation timed out. Please try again.");
        }
        catch (BridgeTaskException ex)
        {
            AddInLogger.Error($"Bridge task error: {ex.ErrorCode}", ex);
            AssistantPanelFactory.ShowError($"Bridge error: {ex.Message}");
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Bridge communication failed for '{action}'", ex);
            AssistantPanelFactory.ShowError("The local TIA Agent Bridge is not running.\nStart TiaAgent.Bridge and try again.\n\nDetails: " + ex.Message);
        }
    }
}

public sealed class TiaAgentTestContextMenu : ContextMenuAddIn
{
    private readonly TiaPortal _tiaPortal;

    public TiaAgentTestContextMenu(TiaPortal tiaPortal) : base("TIA Agent Diagnostics")
    {
        _tiaPortal = tiaPortal;
    }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Test Integration",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
            {
                try
                {
                    AddInLogger.Info("Test Integration action triggered");

                    var version = typeof(TiaAgentTestContextMenu).Assembly.GetName().Version?.ToString() ?? "unknown";
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

                    var msg = "TIA Portal Code Agent - Integration Test\n"
                            + "==========================================\n\n"
                            + "Status:    LOADED AND FUNCTIONAL\n"
                            + "Version:   " + version + "\n"
                            + "Timestamp: " + timestamp + "\n"
                            + "Process:   " + pid + "\n\n"
                            + "The Add-In is correctly installed and operational.\n"
                            + "Context menu actions are responding.\n\n"
                            + "Bridge: " + AddInServices.Config.BridgeBaseUrl + "\n"
                            + "Log: " + Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "TiaAgent", "addin.log");

                    AddInLogger.Info($"Test Integration passed - v{version} PID={pid}");
                    MessageBox.Show(msg, "TIA Agent - Integration Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AddInLogger.Error("Test Integration failed", ex);
                    MessageBox.Show("Integration test failed: " + ex.Message,
                        "TIA Agent - Integration Test", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
    }
}
#endif
```

- [ ] **Step 6: Create SelectionSnapshotFactory**

Create `src/TiaAgent.AddIn/Providers/SelectionSnapshotFactory.cs`:
```csharp
#if SIEMENS
using System;
using System.Reflection;
using Siemens.Engineering;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Providers;

public static class SelectionSnapshotFactory
{
    public static SelectionSnapshot Create(IEngineeringObject selectedObject, TiaPortal tiaPortal)
    {
        var snapshot = new SelectionSnapshot
        {
            Name = selectedObject.ToString() ?? "Unknown",
            ObjectType = selectedObject.GetType().Name,
            RuntimeType = selectedObject.GetType().FullName ?? "Unknown"
        };

        // Attempt to extract additional properties via reflection
        snapshot.TrySetProperty(selectedObject, "PlcName", ref snapshot.PlcName);
        snapshot.TrySetProperty(selectedObject, "Name", ref snapshot.Name);
        snapshot.TrySetProperty(selectedObject, "Path", ref snapshot.TiaPath);

        // Try to get language from block properties
        var languageProp = selectedObject.GetType().GetProperty("Language");
        if (languageProp != null)
        {
            try
            {
                var langVal = languageProp.GetValue(selectedObject);
                if (langVal != null)
                    snapshot = snapshot.WithLanguage(langVal.ToString());
            }
            catch (Exception ex)
            {
                AddInLogger.Warn($"Failed to read Language property: {ex.Message}");
            }
        }

        // Try to get project info
        try
        {
            var parentProject = GetParentProject(selectedObject);
            if (parentProject != null)
            {
                var pathProp = parentProject.GetType().GetProperty("Path");
                if (pathProp != null)
                {
                    var path = pathProp.GetValue(parentProject);
                    if (path != null)
                        snapshot = snapshot.WithTiaPath(path.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to read project info: {ex.Message}");
        }

        return snapshot;
    }

    private static IEngineeringObject? GetParentProject(IEngineeringObject obj)
    {
        var parentProp = obj.GetType().GetProperty("Parent");
        if (parentProp == null) return null;

        var current = obj;
        while (current != null)
        {
            try
            {
                var parent = parentProp.GetValue(current) as IEngineeringObject;
                if (parent == null) return null;

                if (parent is Project)
                    return parent;

                current = parent;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static void TrySetProperty(this SelectionSnapshot snapshot, IEngineeringObject obj, string propName, ref string target)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val != null)
                    target = val.ToString() ?? target;
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to read {propName}: {ex.Message}");
        }
    }

    private static SelectionSnapshot WithLanguage(this SelectionSnapshot snapshot, string? language)
    {
        return new SelectionSnapshot
        {
            Name = snapshot.Name,
            ObjectType = snapshot.ObjectType,
            RuntimeType = snapshot.RuntimeType,
            PlcName = snapshot.PlcName,
            TiaPath = snapshot.TiaPath,
            Language = language
        };
    }

    private static SelectionSnapshot WithTiaPath(this SelectionSnapshot snapshot, string? tiaPath)
    {
        return new SelectionSnapshot
        {
            Name = snapshot.Name,
            ObjectType = snapshot.ObjectType,
            RuntimeType = snapshot.RuntimeType,
            PlcName = snapshot.PlcName,
            TiaPath = tiaPath ?? snapshot.TiaPath,
            Language = snapshot.Language
        };
    }
}
#endif
```

- [ ] **Step 7: Update Config.xml to remove obsolete assemblies**

Edit `src/TiaAgent.AddIn/Config.xml`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<PackageConfiguration xmlns="http://www.siemens.com/automation/Openness/AddIn/Publisher/V21">
  <Author>TIA Portal Code Agent</Author>
  <AddInVersion>V21</AddInVersion>
  <Description>AI-powered engineering assistant for TIA Portal V21</Description>
  <DisplayInMultiuser />

  <Product>
    <Name>TIA Portal Code Agent</Name>
    <Id>tia_portal_code_agent</Id>
    <Version>0.1.0</Version>
  </Product>

  <FeatureAssembly>
    <AssemblyInfo>
      <Assembly>TiaAgent.AddIn.dll</Assembly>
    </AssemblyInfo>
  </FeatureAssembly>

  <AdditionalAssemblies>
    <AssemblyInfo>
      <Assembly>TiaAgent.Contracts.dll</Assembly>
    </AssemblyInfo>
  </AdditionalAssemblies>

  <RequiredPermissions>
    <TIAPermissions>
      <TIA.ReadWrite/>
    </TIAPermissions>
    <UnrestrictedPermissions>
      <System.UnrestrictedAccess>
        <JustificationComment>Needed for file I/O, logging, and network access for Bridge communication</JustificationComment>
      </System.UnrestrictedAccess>
    </UnrestrictedPermissions>
  </RequiredPermissions>
</PackageConfiguration>
```

- [ ] **Step 8: Build and verify**

Run: `dotnet build TiaAgent.sln --configuration Release`
Verify: TiaAgent.AddIn.dll does NOT reference TiaAgent.Application or TiaAgent.OpenCode.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "refactor: remove modern dependencies from Add-In, use Bridge client"
```

---

## Task 6: Create TiaAgent.Bridge Project

**Covers:** Phase 6 (Bridge application)
**Files:**
- Create: `src/TiaAgent.Bridge/TiaAgent.Bridge.csproj`
- Create: `src/TiaAgent.Bridge/Program.cs`
- Create: `src/TiaAgent.Bridge/Api/BridgeController.cs`
- Create: `src/TiaAgent.Bridge/Sessions/SessionManager.cs`
- Create: `src/TiaAgent.Bridge/Tasks/TaskManager.cs`
- Create: `src/TiaAgent.Bridge/OpenCode/OpenCodeClient.cs`
- Create: `src/TiaAgent.Bridge/Security/TokenProvider.cs`
- Create: `src/TiaAgent.Bridge/Diagnostics/BridgeLogger.cs`
- Create: `src/TiaAgent.Bridge/Configuration/BridgeConfig.cs`

**Interfaces:**
- Consumes: All Bridge contracts from TiaAgent.Contracts
- Produces: Bridge HTTP API on 127.0.0.1:43119

- [ ] **Step 1: Create Bridge csproj**

Create `src/TiaAgent.Bridge/TiaAgent.Bridge.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>TiaAgent.Bridge</RootNamespace>
    <Description>Local Bridge between TIA Portal Add-In and OpenCode Server</Description>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TiaAgent.Contracts\TiaAgent.Contracts.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create BridgeLogger**

Create `src/TiaAgent.Bridge/Diagnostics/BridgeLogger.cs`:
```csharp
using System;
using System.IO;

namespace TiaAgent.Bridge.Diagnostics;

public static class BridgeLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

    private static readonly string LogFile = Path.Combine(LogDir, "bridge.log");
    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var text = ex != null ? $"{message}: {ex.Message}" : message;
        Write("ERROR", text);
    }

    public static void Debug(string message) => Write("DEBUG", message);

    public static void Startup()
    {
        Write("INFO", "=== TIA Agent Bridge starting ===");
        Write("INFO", $"Version: 0.1.0");
        Write("INFO", $"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
        Write("INFO", $".NET: {Environment.Version}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
        }
        catch
        {
            // Swallow — logging must never crash the Bridge
        }
    }
}
```

- [ ] **Step 3: Create BridgeConfig**

Create `src/TiaAgent.Bridge/Configuration/BridgeConfig.cs`:
```csharp
using System;
using System.IO;

namespace TiaAgent.Bridge.Configuration;

public sealed class BridgeConfig
{
    public int Port { get; set; } = 43119;
    public string OpenCodeBaseUrl { get; set; } = "http://127.0.0.1:43120";
    public int TaskTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentTasks { get; set; } = 5;
    public int MaxRequestBodyBytes { get; set; } = 1048576;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "bridge.json");

    public static BridgeConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFile))
            {
                var defaultConfig = new BridgeConfig();
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigFile);
            return Deserialize(json);
        }
        catch
        {
            return new BridgeConfig();
        }
    }

    public static void Save(BridgeConfig config)
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            var json = Serialize(config);
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
            // Best effort
        }
    }

    private static string Serialize(BridgeConfig config)
    {
        return "{\"port\":" + config.Port + ","
             + "\"openCodeBaseUrl\":\"" + EscapeJson(config.OpenCodeBaseUrl) + "\","
             + "\"taskTimeoutSeconds\":" + config.TaskTimeoutSeconds + ","
             + "\"maxConcurrentTasks\":" + config.MaxConcurrentTasks + ","
             + "\"maxRequestBodyBytes\":" + config.MaxRequestBodyBytes + "}";
    }

    private static BridgeConfig Deserialize(string json)
    {
        var config = new BridgeConfig();
        config.Port = ExtractInt(json, "port") ?? config.Port;
        config.OpenCodeBaseUrl = ExtractString(json, "openCodeBaseUrl") ?? config.OpenCodeBaseUrl;
        config.TaskTimeoutSeconds = ExtractInt(json, "taskTimeoutSeconds") ?? config.TaskTimeoutSeconds;
        config.MaxConcurrentTasks = ExtractInt(json, "maxConcurrentTasks") ?? config.MaxConcurrentTasks;
        config.MaxRequestBodyBytes = ExtractInt(json, "maxRequestBodyBytes") ?? config.MaxRequestBodyBytes;
        return config;
    }

    private static string? ExtractString(string json, string key)
    {
        var searchKey = "\"" + key + "\":\"";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private static int? ExtractInt(string json, string key)
    {
        var searchKey = "\"" + key + "\":";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = start;
        while (end < json.Length && char.IsDigit(json[end])) end++;
        if (end == start) return null;
        if (int.TryParse(json.Substring(start, end - start), out var val))
            return val;
        return null;
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
```

- [ ] **Step 4: Create TokenProvider**

Create `src/TiaAgent.Bridge/Security/TokenProvider.cs`:
```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TiaAgent.Bridge.Security;

public sealed class TokenProvider
{
    private readonly string _token;
    private static readonly string TokenDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");
    private static readonly string TokenFile = Path.Combine(TokenDir, "bridge.token");

    public TokenProvider()
    {
        _token = LoadOrCreateToken();
    }

    public string Token => _token;

    public bool Validate(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        return string.Equals(_token, token, StringComparison.Ordinal);
    }

    private string LoadOrCreateToken()
    {
        try
        {
            if (File.Exists(TokenFile))
                return File.ReadAllText(TokenFile).Trim();

            var token = GenerateToken();

            if (!Directory.Exists(TokenDir))
                Directory.CreateDirectory(TokenDir);

            File.WriteAllText(TokenFile, token);
            return token;
        }
        catch
        {
            return GenerateToken();
        }
    }

    private string GenerateToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
```

- [ ] **Step 5: Create SessionManager**

Create `src/TiaAgent.Bridge/Sessions/SessionManager.cs`:
```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;

namespace TiaAgent.Bridge.Sessions;

public sealed class SessionManager
{
    private readonly OpenCodeClient _openCodeClient;
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private int _sessionCounter;

    public SessionManager(OpenCodeClient openCodeClient)
    {
        _openCodeClient = openCodeClient;
    }

    public async Task<string> GetOrCreateSessionAsync(
        string projectKey,
        string correlationId,
        string? agentId,
        CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(projectKey, out var existing))
        {
            existing.LastUsedAt = DateTime.UtcNow;
            BridgeLogger.Debug($"Reusing session {existing.SessionId} for project {projectKey}");
            return existing.SessionId;
        }

        BridgeLogger.Info($"Creating new session for project {projectKey} (correlationId={correlationId})");

        var sessionId = await _openCodeClient.CreateSessionAsync(
            correlationId,
            projectKey,
            agentId ?? "tia-explain",
            cancellationToken);

        var entry = new SessionEntry
        {
            SessionId = sessionId,
            ProjectKey = projectKey,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };

        _sessions[projectKey] = entry;
        BridgeLogger.Info($"Session created: {sessionId} for project {projectKey}");

        return sessionId;
    }

    public void RemoveSession(string projectKey)
    {
        if (_sessions.TryRemove(projectKey, out var entry))
        {
            BridgeLogger.Info($"Session removed: {entry.SessionId} for project {projectKey}");
        }
    }

    public int ActiveSessionCount => _sessions.Count;

    private sealed class SessionEntry
    {
        public string SessionId { get; set; } = "";
        public string ProjectKey { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
    }
}
```

- [ ] **Step 6: Create TaskManager**

Create `src/TiaAgent.Bridge/Tasks/TaskManager.cs`:
```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Bridge.Sessions;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.Bridge.Tasks;

public sealed class TaskManager
{
    private readonly SessionManager _sessionManager;
    private readonly OpenCodeClient _openCodeClient;
    private readonly ConcurrentDictionary<string, TaskEntry> _tasks = new();
    private int _taskCounter;
    private int _activeTasks;
    private readonly int _maxConcurrentTasks;

    public TaskManager(
        SessionManager sessionManager,
        OpenCodeClient openCodeClient,
        int maxConcurrentTasks)
    {
        _sessionManager = sessionManager;
        _openCodeClient = openCodeClient;
        _maxConcurrentTasks = maxConcurrentTasks;
    }

    public int ActiveTaskCount => _activeTasks;

    public async Task<BridgeTaskAccepted> CreateTaskAsync(
        BridgeTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (_activeTasks >= _maxConcurrentTasks)
        {
            throw new TaskLimitException("Maximum concurrent tasks reached");
        }

        var taskId = $"bridge-{Interlocked.Increment(ref _taskCounter)}";
        var projectKey = NormalizeProjectKey(request.Project.Path, request.TiaInstance.ProcessId, request.Project.Name);

        var entry = new TaskEntry
        {
            TaskId = taskId,
            CorrelationId = request.CorrelationId,
            Action = request.Action,
            Status = BridgeTaskStatusValues.Pending,
            ProjectKey = projectKey,
            Request = request,
            CreatedAt = DateTime.UtcNow
        };

        _tasks[taskId] = entry;
        Interlocked.Increment(ref _activeTasks);

        BridgeLogger.Info($"Task created: {taskId} (action={request.Action}, correlationId={request.CorrelationId})");

        // Start task execution in background
        _ = Task.Run(() => ExecuteTaskAsync(entry, cancellationToken));

        return new BridgeTaskAccepted
        {
            TaskId = taskId,
            Status = BridgeTaskStatusValues.Pending,
            CorrelationId = request.CorrelationId
        };
    }

    public BridgeTaskStatus? GetTaskStatus(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var entry))
            return null;

        return new BridgeTaskStatus
        {
            TaskId = entry.TaskId,
            Status = entry.Status,
            Stage = entry.Stage,
            Message = entry.Message,
            Response = entry.Response,
            Error = entry.Error
        };
    }

    public bool CancelTask(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var entry))
            return false;

        if (entry.Status == BridgeTaskStatusValues.Completed ||
            entry.Status == BridgeTaskStatusValues.Failed ||
            entry.Status == BridgeTaskStatusValues.Cancelled)
        {
            return false;
        }

        entry.Cts?.Cancel();
        entry.Status = BridgeTaskStatusValues.Cancelled;
        entry.Message = "Cancelled by user";

        BridgeLogger.Info($"Task cancelled: {taskId}");
        Interlocked.Decrement(ref _activeTasks);
        return true;
    }

    private async Task ExecuteTaskAsync(TaskEntry entry, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        entry.Cts = cts;

        try
        {
            entry.Status = BridgeTaskStatusValues.Running;
            entry.Stage = "initializing";

            // Get or create session
            var sessionId = await _sessionManager.GetOrCreateSessionAsync(
                entry.ProjectKey,
                entry.CorrelationId,
                entry.Request.AgentId,
                cts.Token);

            entry.Stage = "processing";

            // Build prompt for OpenCode
            var prompt = BuildPrompt(entry.Request);

            // Send to OpenCode
            var result = await _openCodeClient.SendMessageAsync(
                sessionId,
                prompt,
                cts.Token);

            if (result.Success)
            {
                entry.Status = BridgeTaskStatusValues.Completed;
                entry.Response = result.Response;
                entry.Stage = "completed";
                BridgeLogger.Info($"Task completed: {entry.TaskId}");
            }
            else
            {
                entry.Status = BridgeTaskStatusValues.Failed;
                entry.Error = new BridgeError
                {
                    Code = result.ErrorCode ?? "OPENCODE_TASK_FAILED",
                    Message = result.ErrorMessage ?? "Task failed",
                    Retryable = true
                };
                entry.Stage = "failed";
                BridgeLogger.Warn($"Task failed: {entry.TaskId} - {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            if (entry.Status != BridgeTaskStatusValues.Cancelled)
            {
                entry.Status = BridgeTaskStatusValues.Cancelled;
                entry.Message = "Cancelled";
            }
        }
        catch (Exception ex)
        {
            entry.Status = BridgeTaskStatusValues.Failed;
            entry.Error = new BridgeError
            {
                Code = "INTERNAL_ERROR",
                Message = ex.Message,
                Retryable = false
            };
            entry.Stage = "failed";
            BridgeLogger.Error($"Task error: {entry.TaskId}", ex);
        }
        finally
        {
            Interlocked.Decrement(ref _activeTasks);
            cts.Dispose();
        }
    }

    private string BuildPrompt(BridgeTaskRequest request)
    {
        var selection = request.Selection;
        var actionDescription = request.Action switch
        {
            "explain" => "Explain the selected object.",
            "review" => "Review the selected object for correctness, maintainability, PLC scan-cycle behavior, determinism, diagnostics, naming, and industrial automation risks.",
            "propose" => "Analyze the selected object and propose a change. Do not apply any write operation automatically.",
            _ => "Analyze the selected object."
        };

        return $@"The user selected an object inside Siemens TIA Portal V21.

Selection metadata:
- Project: {request.Project.Name}
- Project path: {request.Project.Path}
- Object name: {selection.Name}
- Object type: {selection.ObjectType}
- Runtime type: {selection.RuntimeType}
{(string.IsNullOrEmpty(selection.PlcName) ? "" : $"- PLC: {selection.PlcName}")}
{(string.IsNullOrEmpty(selection.TiaPath) ? "" : $"- TIA path: {selection.TiaPath}")}
{(string.IsNullOrEmpty(selection.Language) ? "" : $"- Language: {selection.Language}")}

Task:
{actionDescription}

Rules:
1. Treat the selection metadata only as a hint.
2. Verify the project and object through the configured tia-portal MCP tools.
3. Use browse_project_tree before reading block content when the exact MCP path is not known.
4. Use the deterministic path returned by the MCP server.
5. {(request.Action == "explain" ? "Do not modify the project." : request.Action == "review" ? "Do not modify the project." : "When a change is appropriate: describe the change, explain risks, prepare a preview, and wait for explicit user approval before any apply operation.")}
6. Clearly distinguish verified project facts from your own interpretation.
7. Report when the object cannot be found or is ambiguous.";
    }

    private string NormalizeProjectKey(string? projectPath, int processId, string projectName)
    {
        if (!string.IsNullOrEmpty(projectPath))
            return projectPath.ToLowerInvariant();

        return $"{processId}:{projectName}";
    }

    private sealed class TaskEntry
    {
        public string TaskId { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public string Action { get; set; } = "";
        public string Status { get; set; } = "";
        public string Stage { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Response { get; set; }
        public BridgeError? Error { get; set; }
        public string ProjectKey { get; set; } = "";
        public BridgeTaskRequest Request { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public CancellationTokenSource? Cts { get; set; }
    }
}

public class TaskLimitException : Exception
{
    public TaskLimitException(string message) : base(message) { }
}
```

- [ ] **Step 7: Create OpenCodeClient**

Create `src/TiaAgent.Bridge/OpenCode/OpenCodeClient.cs`:
```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Diagnostics;

namespace TiaAgent.Bridge.OpenCode;

public sealed class OpenCodeClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OpenCodeClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/global/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            BridgeLogger.Debug($"OpenCode health check failed: {ex.Message}");
            return false;
        }
    }

    public async Task<string> CreateSessionAsync(
        string correlationId,
        string projectId,
        string agentId,
        CancellationToken cancellationToken)
    {
        var payload = $"{{\"correlationId\":\"{EscapeJson(correlationId)}\",\"projectId\":\"{EscapeJson(projectId)}\",\"agentId\":\"{EscapeJson(agentId)}\"}}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/session", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return ExtractString(json, "sessionId") ?? throw new InvalidOperationException("No session ID in response");
    }

    public async Task<OpenCodeResult> SendMessageAsync(
        string sessionId,
        string message,
        CancellationToken cancellationToken)
    {
        var payload = $"{{\"message\":\"{EscapeJson(message)}\"}}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"/session/{sessionId}/message", content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new OpenCodeResult
            {
                Success = false,
                ErrorCode = "OPENCODE_TASK_FAILED",
                ErrorMessage = ExtractString(json, "error") ?? "OpenCode request failed"
            };
        }

        return new OpenCodeResult
        {
            Success = true,
            Response = ExtractString(json, "response") ?? ExtractString(json, "message") ?? json
        };
    }

    public async Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await _httpClient.PostAsync($"/session/{sessionId}/abort", null, cancellationToken);
        }
        catch (Exception ex)
        {
            BridgeLogger.Warn($"Failed to abort session {sessionId}: {ex.Message}");
        }
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private string? ExtractString(string json, string key)
    {
        var searchKey = "\"" + key + "\":\"";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public class OpenCodeResult
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
```

- [ ] **Step 8: Create BridgeController**

Create `src/TiaAgent.Bridge/Api/BridgeController.cs`:
```csharp
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Sessions;
using TiaAgent.Bridge.Tasks;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.Bridge.Api;

public sealed class BridgeController
{
    private readonly TaskManager _taskManager;
    private readonly SessionManager _sessionManager;
    private readonly TokenProvider _tokenProvider;
    private readonly BridgeConfig _config;
    private readonly DateTime _startTime;

    public BridgeController(
        TaskManager taskManager,
        SessionManager sessionManager,
        TokenProvider tokenProvider,
        BridgeConfig config)
    {
        _taskManager = taskManager;
        _sessionManager = sessionManager;
        _tokenProvider = tokenProvider;
        _config = config;
        _startTime = DateTime.UtcNow;
    }

    public async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // CORS headers for local access
            response.Headers.Add("Access-Control-Allow-Origin", "http://127.0.0.1");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                return;
            }

            // Authentication
            if (!AuthenticateRequest(request))
            {
                await WriteErrorResponseAsync(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Invalid or missing authentication token");
                return;
            }

            var path = request.Url?.AbsolutePath ?? "";

            // Route requests
            if (path == "/health" && request.HttpMethod == "GET")
            {
                await HandleHealthAsync(response);
            }
            else if (path == "/v1/tasks" && request.HttpMethod == "POST")
            {
                await HandleCreateTaskAsync(request, response, cancellationToken);
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(path, @"^/v1/tasks/[^/]+$") && request.HttpMethod == "GET")
            {
                var taskId = path.Split('/')[3];
                await HandleGetTaskAsync(taskId, response);
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(path, @"^/v1/tasks/[^/]+/cancel$") && request.HttpMethod == "POST")
            {
                var taskId = path.Split('/')[3];
                await HandleCancelTaskAsync(taskId, response);
            }
            else if (path == "/diagnostics" && request.HttpMethod == "GET")
            {
                await HandleDiagnosticsAsync(response);
            }
            else
            {
                await WriteErrorResponseAsync(response, HttpStatusCode.NotFound, "NOT_FOUND", "Endpoint not found");
            }
        }
        catch (Exception ex)
        {
            BridgeLogger.Error($"Request error: {request.Url}", ex);
            await WriteErrorResponseAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Internal server error");
        }
        finally
        {
            response.Close();
        }
    }

    private bool AuthenticateRequest(HttpListenerRequest request)
    {
        var authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader))
            return false;

        var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;
        return _tokenProvider.Validate(token);
    }

    private async Task HandleHealthAsync(HttpListenerResponse response)
    {
        var openCodeAvailable = false;
        try
        {
            // Health check would go through OpenCodeClient
            openCodeAvailable = true; // Simplified for MVP
        }
        catch { }

        var health = new BridgeHealthResponse
        {
            Status = "healthy",
            BridgeVersion = "0.1.0",
            OpenCodeAvailable = openCodeAvailable,
            OpenCodeVersion = "unknown",
            McpConfigured = true
        };

        await WriteJsonResponseAsync(response, HttpStatusCode.OK, SerializeHealthResponse(health));
    }

    private async Task HandleCreateTaskAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken cancellationToken)
    {
        var body = await ReadRequestBodyAsync(request);
        var taskRequest = DeserializeTaskRequest(body);

        if (taskRequest == null)
        {
            await WriteErrorResponseAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST", "Invalid request body");
            return;
        }

        try
        {
            var accepted = await _taskManager.CreateTaskAsync(taskRequest, cancellationToken);
            await WriteJsonResponseAsync(response, HttpStatusCode.Accepted, SerializeTaskAccepted(accepted));
        }
        catch (TaskLimitException)
        {
            await WriteErrorResponseAsync(response, HttpStatusCode.ServiceUnavailable, "TASK_LIMIT", "Maximum concurrent tasks reached");
        }
    }

    private async Task HandleGetTaskAsync(string taskId, HttpListenerResponse response)
    {
        var status = _taskManager.GetTaskStatus(taskId);
        if (status == null)
        {
            await WriteErrorResponseAsync(response, HttpStatusCode.NotFound, "TASK_NOT_FOUND", "Task not found");
            return;
        }

        await WriteJsonResponseAsync(response, HttpStatusCode.OK, SerializeTaskStatus(status));
    }

    private async Task HandleCancelTaskAsync(string taskId, HttpListenerResponse response)
    {
        var cancelled = _taskManager.CancelTask(taskId);
        if (!cancelled)
        {
            await WriteErrorResponseAsync(response, HttpStatusCode.NotFound, "TASK_NOT_FOUND", "Task not found or already completed");
            return;
        }

        await WriteJsonResponseAsync(response, HttpStatusCode.OK, "{\"status\":\"cancelled\"}");
    }

    private async Task HandleDiagnosticsAsync(HttpListenerResponse response)
    {
        var diagnostics = new
        {
            bridgeVersion = "0.1.0",
            uptime = (DateTime.UtcNow - _startTime).ToString(),
            openCodeUrl = _config.OpenCodeBaseUrl,
            activeTasks = _taskManager.ActiveTaskCount,
            activeSessions = _sessionManager.ActiveSessionCount,
            maxConcurrentTasks = _config.MaxConcurrentTasks
        };

        await WriteJsonResponseAsync(response, HttpStatusCode.OK, SerializeDiagnostics(diagnostics));
    }

    private async Task WriteJsonResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string json)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private async Task WriteErrorResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string code, string message)
    {
        var error = $"{{\"code\":\"{code}\",\"message\":\"{message}\"}}";
        await WriteJsonResponseAsync(response, statusCode, error);
    }

    private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    private BridgeTaskRequest? DeserializeTaskRequest(string json)
    {
        try
        {
            return new BridgeTaskRequest
            {
                CorrelationId = ExtractString(json, "correlationId") ?? "",
                Action = ExtractString(json, "action") ?? "",
                AgentId = ExtractString(json, "agentId") ?? "",
                UserMessage = ExtractString(json, "userMessage") ?? "",
                TiaInstance = new TiaInstanceSnapshot
                {
                    ProcessId = ExtractInt(json, "processId") ?? 0,
                    SessionId = ExtractString(json, "sessionId") ?? "",
                    Version = ExtractString(json, "version") ?? ""
                },
                Project = new ProjectSnapshot
                {
                    Id = ExtractString(json, "projectId") ?? "",
                    Name = ExtractString(json, "projectName") ?? "",
                    Path = ExtractString(json, "projectPath") ?? ""
                },
                Selection = new SelectionSnapshot
                {
                    Name = ExtractString(json, "selectionName") ?? "",
                    ObjectType = ExtractString(json, "objectType") ?? "",
                    RuntimeType = ExtractString(json, "runtimeType") ?? "",
                    PlcName = ExtractString(json, "plcName") ?? "",
                    TiaPath = ExtractString(json, "tiaPath") ?? "",
                    Language = ExtractString(json, "language") ?? ""
                }
            };
        }
        catch
        {
            return null;
        }
    }

    private string SerializeHealthResponse(BridgeHealthResponse health)
    {
        return $"{{\"status\":\"{health.Status}\",\"bridgeVersion\":\"{health.BridgeVersion}\",\"openCodeAvailable\":{health.OpenCodeAvailable.ToString().ToLower()},\"openCodeVersion\":\"{health.OpenCodeVersion}\",\"mcpConfigured\":{health.McpConfigured.ToString().ToLower()}}}";
    }

    private string SerializeTaskAccepted(BridgeTaskAccepted accepted)
    {
        return $"{{\"taskId\":\"{accepted.TaskId}\",\"status\":\"{accepted.Status}\",\"correlationId\":\"{accepted.CorrelationId}\"}}";
    }

    private string SerializeTaskStatus(BridgeTaskStatus status)
    {
        var errorJson = status.Error != null
            ? $"{{\"code\":\"{status.Error.Code}\",\"message\":\"{status.Error.Message}\",\"retryable\":{status.Error.Retryable.ToString().ToLower()}}}"
            : "null";

        return $"{{\"taskId\":\"{status.TaskId}\",\"status\":\"{status.Status}\",\"stage\":\"{status.Stage}\",\"message\":\"{status.Message}\",\"response\":\"{EscapeJson(status.Response ?? "")}\",\"error\":{errorJson}}}";
    }

    private string SerializeDiagnostics(object diagnostics)
    {
        return System.Text.Json.JsonSerializer.Serialize(diagnostics);
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private string? ExtractString(string json, string key)
    {
        var searchKey = "\"" + key + "\":\"";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private int? ExtractInt(string json, string key)
    {
        var searchKey = "\"" + key + "\":";
        var start = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (start < 0) return null;
        start += searchKey.Length;
        var end = start;
        while (end < json.Length && char.IsDigit(json[end])) end++;
        if (end == start) return null;
        if (int.TryParse(json.Substring(start, end - start), out var val))
            return val;
        return null;
    }
}
```

- [ ] **Step 9: Create Program.cs**

Create `src/TiaAgent.Bridge/Program.cs`:
```csharp
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Bridge.Api;
using TiaAgent.Bridge.Configuration;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.OpenCode;
using TiaAgent.Bridge.Security;
using TiaAgent.Bridge.Sessions;
using TiaAgent.Bridge.Tasks;

namespace TiaAgent.Bridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        BridgeLogger.Startup();

        var config = BridgeConfig.Load();
        var tokenProvider = new TokenProvider();

        BridgeLogger.Info($"Bridge configuration loaded: port={config.Port}");

        var openCodeClient = new OpenCodeClient(config.OpenCodeBaseUrl);
        var sessionManager = new SessionManager(openCodeClient);
        var taskManager = new TaskManager(sessionManager, openCodeClient, config.MaxConcurrentTasks);

        var controller = new BridgeController(taskManager, sessionManager, tokenProvider, config);

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{config.Port}/");

        try
        {
            listener.Start();
            BridgeLogger.Info($"Bridge listening on http://127.0.0.1:{config.Port}");
            BridgeLogger.Info($"Authentication token: {tokenProvider.Token}");
            BridgeLogger.Info("Bridge is ready");

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                BridgeLogger.Info("Shutdown requested");
                cts.Cancel();
            };

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => controller.HandleRequestAsync(context, cts.Token));
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            BridgeLogger.Error("Bridge failed to start", ex);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            openCodeClient.Dispose();
            BridgeLogger.Info("Bridge stopped");
        }
    }
}
```

- [ ] **Step 10: Update solution file**

Add TiaAgent.Bridge to `TiaAgent.sln`.

- [ ] **Step 11: Build and verify**

Run: `dotnet build src/TiaAgent.Bridge/TiaAgent.Bridge.csproj --configuration Release`

- [ ] **Step 12: Commit**

```bash
git add src/TiaAgent.Bridge/
git commit -m "feat: create TiaAgent.Bridge (.NET 8) with HTTP API"
```

---

## Task 7: Create Architecture Tests

**Covers:** Phase 18 (Tests)
**Files:**
- Modify: `tests/TiaAgent.ArchitectureTests/TiaAgent.ArchitectureTests.csproj`
- Modify: `tests/TiaAgent.ArchitectureTests/DependencyTests.cs`

**Interfaces:**
- Consumes: All projects in solution
- Produces: Test results verifying architectural constraints

- [ ] **Step 1: Update ArchitectureTests csproj**

Edit `tests/TiaAgent.ArchitectureTests/TiaAgent.ArchitectureTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>TiaAgent.ArchitectureTests</RootNamespace>
    <IsPackable>false</IsPackable>
    <NoWarn>$(NoWarn);NU1702</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\TiaAgent.Contracts\TiaAgent.Contracts.csproj" />
    <ProjectReference Include="..\..\src\TiaAgent.Application\TiaAgent.Application.csproj" />
    <ProjectReference Include="..\..\src\TiaAgent.AddIn\TiaAgent.AddIn.csproj" />
    <ProjectReference Include="..\..\src\TiaAgent.OpenCode\TiaAgent.OpenCode.csproj" />
    <ProjectReference Include="..\..\src\TiaAgent.Bridge\TiaAgent.Bridge.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add comprehensive architecture tests**

Replace `tests/TiaAgent.ArchitectureTests/DependencyTests.cs`:
```csharp
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

public class DependencyTests
{
    [Fact]
    public void Contracts_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Contracts must not reference Siemens assemblies");
    }

    [Fact]
    public void Application_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Application.Common.GuidIdGenerator).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Application must not reference Siemens assemblies");
    }

    [Fact]
    public void Contracts_ShouldTargetNetStandard()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

        targetFramework?.FrameworkName.Should().Contain("NETStandard",
            "TiaAgent.Contracts should target netstandard2.0 for cross-framework compatibility");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceOpenCode()
    {
        var assembly = typeof(TiaAgent.AddIn.Diagnostics.AddInLogger).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "TiaAgent.OpenCode",
            "TiaAgent.AddIn must not reference TiaAgent.OpenCode");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceApplication()
    {
        var assembly = typeof(TiaAgent.AddIn.Diagnostics.AddInLogger).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "TiaAgent.Application",
            "TiaAgent.AddIn must not reference TiaAgent.Application");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceMicrosoftExtensionsLogging()
    {
        var assembly = typeof(TiaAgent.AddIn.Diagnostics.AddInLogger).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "Microsoft.Extensions.Logging.Abstractions",
            "TiaAgent.AddIn must not reference Microsoft.Extensions.Logging.Abstractions");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceMicrosoftExtensionsDependencyInjection()
    {
        var assembly = typeof(TiaAgent.AddIn.Diagnostics.AddInLogger).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "Microsoft.Extensions.DependencyInjection.Abstractions",
            "TiaAgent.AddIn must not reference Microsoft.Extensions.DependencyInjection.Abstractions");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceMicrosoftBclAsyncInterfaces()
    {
        var assembly = typeof(TiaAgent.AddIn.Diagnostics.AddInLogger).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "Microsoft.Bcl.AsyncInterfaces",
            "TiaAgent.AddIn must not reference Microsoft.Bcl.AsyncInterfaces");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceSystemTextJson()
    {
        var assembly = typeof(TiaAgent.AddIn.Diagnostics.AddInLogger).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "System.Text.Json",
            "TiaAgent.AddIn must not reference System.Text.Json");
    }

    [Fact]
    public void Contracts_ShouldNotReferenceMicrosoftExtensions()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name!.StartsWith("Microsoft.Extensions."),
            "TiaAgent.Contracts must not reference Microsoft.Extensions.*");
    }

    [Fact]
    public void Contracts_ShouldNotReferenceSystemTextJson()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "System.Text.Json",
            "TiaAgent.Contracts must not reference System.Text.Json");
    }

    [Fact]
    public void Contracts_ShouldNotReferenceMicrosoftBclAsyncInterfaces()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "Microsoft.Bcl.AsyncInterfaces",
            "TiaAgent.Contracts must not reference Microsoft.Bcl.AsyncInterfaces");
    }

    [Fact]
    public void Bridge_ShouldReferenceContracts()
    {
        var assembly = typeof(TiaAgent.Bridge.Program).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().Contain(r =>
            r.Name == "TiaAgent.Contracts",
            "TiaAgent.Bridge should reference TiaAgent.Contracts");
    }

    [Fact]
    public void Bridge_ShouldTargetNet8()
    {
        var assembly = typeof(TiaAgent.Bridge.Program).Assembly;
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

        targetFramework?.FrameworkName.Should().Contain("NET",
            "TiaAgent.Bridge should target .NET 8");
    }
}
```

- [ ] **Step 3: Build and run tests**

Run: `dotnet test tests/TiaAgent.ArchitectureTests/TiaAgent.ArchitectureTests.csproj --configuration Release`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/TiaAgent.ArchitectureTests/
git commit -m "test: add comprehensive architecture dependency tests"
```

---

## Task 8: Update Build Scripts and Documentation

**Covers:** Phase 16 (Packaging), Phase 20 (Documentation)
**Files:**
- Modify: `build.ps1`
- Modify: `docs/architecture.md` (if exists)

**Interfaces:**
- Produces: Updated build pipeline and documentation

- [ ] **Step 1: Update build.ps1 to include Bridge build**

Update `build.ps1` Invoke-Build function to also build Bridge:
```powershell
function Invoke-Build {
    Write-Header "BUILD"
    Write-Step 1 4 "Compiling solution..."

    dotnet build "$Root\TiaAgent.sln" --configuration $Config --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Write-Fail "Build failed"; exit 1 }
    Write-Ok "Solution compiled"

    Write-Step 2 4 "Verifying Add-In projects..."
    $projects = Get-ChildItem "$Root\src\TiaAgent.AddIn\*.csproj" -ErrorAction SilentlyContinue
    Write-Ok "TiaAgent.AddIn project found"

    Write-Step 3 4 "Verifying Bridge project..."
    $bridge = Get-ChildItem "$Root\src\TiaAgent.Bridge\*.csproj" -ErrorAction SilentlyContinue
    Write-Ok "TiaAgent.Bridge project found"

    Write-Step 4 4 "Verifying artifacts..."
    $addinDll = "$Root\src\TiaAgent.AddIn\bin\$Config\net48\TiaAgent.AddIn.dll"
    if (Test-Path $addinDll) {
        Write-Ok "TiaAgent.AddIn.dll built"
    } else {
        Write-Fail "TiaAgent.AddIn.dll not found at: $addinDll"
        exit 1
    }

    $bridgeExe = "$Root\src\TiaAgent.Bridge\bin\$Config\net8.0\TiaAgent.Bridge.dll"
    if (Test-Path $bridgeExe) {
        Write-Ok "TiaAgent.Bridge.dll built"
    } else {
        Write-Fail "TiaAgent.Bridge.dll not found at: $bridgeExe"
        exit 1
    }

    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
}
```

- [ ] **Step 2: Verify complete build**

Run: `.\build.ps1 build`
Expected: Both Add-In and Bridge build successfully.

- [ ] **Step 3: Commit**

```bash
git add build.ps1
git commit -m "chore: update build script to include Bridge project"
```

---

## Task 9: Clean Up Obsolete Code

**Covers:** Phase 17 (Remove obsolete implementation)
**Files:**
- Remove: Obsolete files in TiaAgent.Application and TiaAgent.OpenCode that are no longer needed
- Modify: Remove empty directories

**Interfaces:**
- Produces: Clean codebase with no dead code

- [ ] **Step 1: Remove obsolete OpenCode orchestrator**

The OpenCodeOrchestrator in TiaAgent.Application is no longer used by the Add-In. Keep it for now as it may be useful for future Bridge integration tests, but mark it as internal.

- [ ] **Step 2: Clean up empty directories**

Remove empty directories in TiaAgent.Application:
- Audit/
- Compatibility/
- Context/
- Hashing/
- Identity/
- Selections/
- Sessions/

- [ ] **Step 3: Build and run all tests**

Run: `dotnet build TiaAgent.sln --configuration Release && dotnet test TiaAgent.sln --configuration Release`
Expected: All builds and tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: clean up empty directories and obsolete code"
```

---

## Task 10: Create Configuration Examples

**Covers:** Phase 10 (OpenCode configuration), Phase 13 (Bridge config)
**Files:**
- Create: `config/bridge.example.json`
- Modify: `config/opencode.example.json`

**Interfaces:**
- Produces: Example configuration files

- [ ] **Step 1: Create bridge.example.json**

Create `config/bridge.example.json`:
```json
{
  "port": 43119,
  "openCodeBaseUrl": "http://127.0.0.1:43120",
  "taskTimeoutSeconds": 300,
  "maxConcurrentTasks": 5,
  "maxRequestBodyBytes": 1048576
}
```

- [ ] **Step 2: Verify opencode.example.json**

The existing `config/opencode.example.json` already has the correct MCP configuration:
```json
{
  "$schema": "https://opencode.ai/config.json",
  "server": {
    "port": 43120
  },
  "mcp": {
    "tia-portal": {
      "type": "local",
      "command": ["tia-mcp"],
      "enabled": true
    }
  }
}
```

This is correct — OpenCode launches tia-mcp via stdio.

- [ ] **Step 3: Commit**

```bash
git add config/
git commit -m "docs: add Bridge configuration example"
```

---

## Task 11: Final Verification and Cleanup

**Covers:** Phase 19 (End-to-end validation)
**Files:**
- Verify: All builds pass
- Verify: All tests pass
- Verify: Add-In package contains only required assemblies

- [ ] **Step 1: Full clean build**

Run: `.\build.ps1 clean && dotnet build TiaAgent.sln --configuration Release`

- [ ] **Step 2: Run all tests**

Run: `dotnet test TiaAgent.sln --configuration Release`
Expected: All tests pass including architecture tests.

- [ ] **Step 3: Verify Add-In output**

Check `src/TiaAgent.AddIn/bin/Release/net48/` contains only:
- TiaAgent.AddIn.dll
- TiaAgent.Contracts.dll
- (NO TiaAgent.Application.dll)
- (NO TiaAgent.OpenCode.dll)
- (NO Microsoft.Extensions.*.dll)
- (NO Microsoft.Bcl.AsyncInterfaces.dll)
- (NO System.Text.Json.dll)

- [ ] **Step 4: Verify Bridge output**

Check `src/TiaAgent.Bridge/bin/Release/net8.0/` contains:
- TiaAgent.Bridge.dll
- TiaAgent.Contracts.dll

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "chore: final verification and cleanup"
```
