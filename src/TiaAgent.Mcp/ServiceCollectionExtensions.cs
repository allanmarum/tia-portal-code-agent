using Microsoft.Extensions.DependencyInjection;
using TiaAgent.Mcp.Tools;

namespace TiaAgent.Mcp;

/// <summary>
/// Extension methods for registering MCP tool handlers and services.
/// The caller is responsible for registering ITiaProjectService and core infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all MCP tool handlers. Requires ITiaProjectService to be registered by the caller.
    /// </summary>
    public static IServiceCollection AddTiaMcpTools(this IServiceCollection services)
    {
        services.AddSingleton<TiaContextTools>();
        services.AddSingleton<TiaReadTools>();
        services.AddSingleton<TiaReferenceTools>();
        services.AddSingleton<TiaCompileTools>();
        services.AddSingleton<TiaChangeTools>();
        services.AddSingleton<TiaDiagnosticTools>();
        return services;
    }
}
