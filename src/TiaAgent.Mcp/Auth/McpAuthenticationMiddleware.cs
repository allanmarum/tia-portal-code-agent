using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TiaAgent.Mcp.Auth;

/// <summary>
/// ASP.NET Core middleware that validates Bearer token authentication on MCP requests.
/// Returns 401 Unauthorized for missing or invalid tokens.
/// Health endpoints are exempt from authentication.
/// </summary>
public sealed class McpAuthenticationMiddleware
{
    private const string BearerPrefix = "Bearer ";
    private readonly RequestDelegate _next;
    private readonly SessionTokenProvider _tokenProvider;
    private readonly ILogger<McpAuthenticationMiddleware> _logger;

    public McpAuthenticationMiddleware(
        RequestDelegate next,
        SessionTokenProvider tokenProvider,
        ILogger<McpAuthenticationMiddleware> logger)
    {
        _next = next;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Exempt health check endpoints from authentication
        var path = context.Request.Path.Value ?? "";
        if (path.Equals("/health", System.StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/healthz", System.StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Extract Bearer token from Authorization header
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            !authHeader.ToString().StartsWith(BearerPrefix, System.StringComparison.Ordinal))
        {
            _logger.LogWarning("MCP request rejected: missing Authorization header (path={Path})", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or invalid Authorization header. Expected: Bearer <token>");
            return;
        }

        var token = authHeader.ToString().Substring(BearerPrefix.Length);

        if (!_tokenProvider.ValidateToken(token))
        {
            _logger.LogWarning("MCP request rejected: invalid token (path={Path})", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid authentication token.");
            return;
        }

        await _next(context);
    }
}
