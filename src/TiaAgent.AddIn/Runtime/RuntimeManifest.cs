using System;
using System.IO;

namespace TiaAgent.AddIn.Runtime;

/// <summary>
/// Runtime manifest model for service discovery.
/// Read-only; only the supervisor writes runtime.json.
/// </summary>
public sealed class RuntimeManifest
{
    public int SchemaVersion { get; set; }
    public string? InstanceId { get; set; }
    public string? Status { get; set; }
    public int SupervisorPid { get; set; }
    public string? StartedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public ServiceInfo? Bridge { get; set; }
    public ServiceInfo? OpenCode { get; set; }
}

/// <summary>
/// Information about a managed service.
/// </summary>
public sealed class ServiceInfo
{
    public string? Status { get; set; }
    public int Pid { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? BaseUrl { get; set; }
    public string? HealthUrl { get; set; }
}
