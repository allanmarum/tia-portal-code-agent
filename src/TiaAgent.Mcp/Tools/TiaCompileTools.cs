using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.Mcp.Tools;

/// <summary>
/// MCP tool handlers for TIA compilation and validation operations.
/// </summary>
public class TiaCompileTools
{
    private readonly ITiaProjectService _projectService;

    public TiaCompileTools(ITiaProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<CompileResultDto> CompileSoftware(
        CompileRequest request,
        CancellationToken cancellationToken)
    {
        return _projectService.CompileSoftwareAsync(request, cancellationToken);
    }
}
