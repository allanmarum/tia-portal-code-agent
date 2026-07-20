using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TiaAgent.Application.Common;
using TiaAgent.Application.Compatibility;
using TiaAgent.Application.Context;
using TiaAgent.Application.Hashing;
using TiaAgent.Application.Identity;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Mcp;
using TiaAgent.Mcp.Auth;
using TiaAgent.Mcp.Tools;
using TiaAgent.Simulator;

var builder = WebApplication.CreateBuilder(args);

// Register authentication (ephemeral bearer token per server lifetime)
builder.Services.AddSingleton<SessionTokenProvider>();

// Register core infrastructure
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();
builder.Services.AddSingleton<IContentHashService, ContentHashService>();

// Register ITiaProjectService — swap to TiaAgent.Openness when TIA Portal is available
builder.Services.AddSingleton<ITiaProjectService, SimulatorTiaProjectService>();

// Register application services
builder.Services.AddSingleton<IContextService, ContextService>();
builder.Services.AddSingleton<ICapabilityProvider, CapabilityProvider>();

// Register MCP tool handlers
builder.Services.AddSingleton<TiaContextTools>();
builder.Services.AddSingleton<TiaReadTools>();
builder.Services.AddSingleton<TiaReferenceTools>();
builder.Services.AddSingleton<TiaCompileTools>();
builder.Services.AddSingleton<TiaChangeTools>();
builder.Services.AddSingleton<TiaDiagnosticTools>();

// Add MCP server with HTTP transport and register tool types
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<TiaContextTools>()
    .WithTools<TiaReadTools>()
    .WithTools<TiaReferenceTools>()
    .WithTools<TiaCompileTools>()
    .WithTools<TiaChangeTools>()
    .WithTools<TiaDiagnosticTools>();

// Configure Kestrel to listen on loopback only
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(43121);
});

var app = builder.Build();

// Display the ephemeral authentication token for configuring clients
var tokenProvider = app.Services.GetRequiredService<SessionTokenProvider>();
Console.WriteLine($"[MCP Auth] Ephemeral token: {tokenProvider.GetToken()}");
Console.WriteLine("[MCP Auth] Configure OpenCode to use: Authorization: Bearer <token>");

// Apply authentication middleware before MCP endpoint
app.UseMiddleware<McpAuthenticationMiddleware>();

app.MapMcp("/mcp");

app.Run();
