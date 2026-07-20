using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA compilation and validation operations.
/// </summary>
[McpServerToolType]
public class TiaCompileTools
{
    private readonly ITiaProjectService _projectService;

    public TiaCompileTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    [McpServerTool(Name = "tia_compile_software", ReadOnly = false)]
    [Description("Compiles the TIA Portal software container and returns compilation messages, errors, and warnings.")]
    public Task<CompileResultDto> CompileSoftware(
        CompileRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.CompileSoftwareAsync(request, cancellationToken);
    }
}
