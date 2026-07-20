using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Errors;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP diagnostic tools for validating the TIA Agent integration.
/// </summary>
[McpServerToolType]
public class TiaDiagnosticTools
{
    private readonly ITiaProjectService _projectService;

    public TiaDiagnosticTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    [McpServerTool(Name = "tia_ping", ReadOnly = true)]
    [Description("Diagnostic tool that validates the MCP server is running and can reach the TIA Portal project service. Returns connection status, project info, and whether a project is currently open.")]
    public async Task<PingResult> Ping(CancellationToken cancellationToken)
    {
        try
        {
            var context = await _projectService.GetCurrentContextAsync(cancellationToken);
            return new PingResult
            {
                Status = "ok",
                Source = "tia-addin",
                TiaConnected = true,
                ProjectOpen = true,
                ProjectName = context.ProjectName,
                TiaVersion = context.TiaVersion
            };
        }
        catch (TiaErrorException ex) when (ex.Error.Code == TiaErrorCode.TIA_NOT_CONNECTED)
        {
            return new PingResult
            {
                Status = "degraded",
                Source = "tia-addin",
                TiaConnected = false,
                ProjectOpen = false
            };
        }
        catch (TiaErrorException ex) when (ex.Error.Code == TiaErrorCode.TIA_PROJECT_NOT_OPEN)
        {
            return new PingResult
            {
                Status = "degraded",
                Source = "tia-addin",
                TiaConnected = true,
                ProjectOpen = false
            };
        }
    }
}

public class PingResult
{
    public required string Status { get; init; }
    public required string Source { get; init; }
    public bool TiaConnected { get; init; }
    public bool ProjectOpen { get; init; }
    public string? ProjectName { get; init; }
    public string? TiaVersion { get; init; }
}
