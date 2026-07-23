using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TiaAgent.Cli.Supervisor;

public sealed class RuntimeManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "starting"; // starting, ready, degraded, stopping, stopped, failed

    [JsonPropertyName("supervisorPid")]
    public int SupervisorPid { get; set; }

    [JsonPropertyName("startedAt")]
    public string StartedAt { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("runtime")]
    public RuntimeIdentityInfo? Runtime { get; set; }

    [JsonPropertyName("services")]
    public RuntimeServicesInfo Services { get; set; } = new();
}

public sealed class RuntimeServicesInfo
{
    [JsonPropertyName("bridge")]
    public ServiceInfo Bridge { get; set; } = new();

    [JsonPropertyName("opencode")]
    public ServiceInfo OpenCode { get; set; } = new();
}

public sealed class ServiceInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, starting, healthy, unhealthy, stopped, failed

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("healthUrl")]
    public string HealthUrl { get; set; } = string.Empty;
}

public sealed class RuntimeIdentityInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "opencode";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "OpenCode";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "server";

    [JsonPropertyName("healthy")]
    public bool Healthy { get; set; }
}

public sealed class SupervisorLockData
{
    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("supervisorPid")]
    public int SupervisorPid { get; set; }

    [JsonPropertyName("startedAt")]
    public string StartedAt { get; set; } = DateTime.UtcNow.ToString("o");
}

public sealed class StatusResult
{
    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    [JsonPropertyName("supervisor")]
    public SupervisorStatusInfo Supervisor { get; set; } = new();

    [JsonPropertyName("bridge")]
    public ServiceStatusInfo Bridge { get; set; } = new();

    [JsonPropertyName("opencode")]
    public ServiceStatusInfo OpenCode { get; set; } = new();

    [JsonPropertyName("runtimePath")]
    public string RuntimePath { get; set; } = string.Empty;
}

public sealed class SupervisorStatusInfo
{
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("pid")]
    public int Pid { get; set; }
}

public sealed class ServiceStatusInfo
{
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("healthy")]
    public bool Healthy { get; set; }

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
