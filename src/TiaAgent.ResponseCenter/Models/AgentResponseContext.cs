namespace TiaAgent.ResponseCenter.Models;

/// <summary>
/// Immutable context captured when the user starts an AI action.
/// Passed from the AddIn to the ResponseCenter via CLI arguments.
/// </summary>
public sealed record AgentResponseContext
{
    /// <summary>The Bridge-assigned task ID.</summary>
    public required string TaskId { get; init; }

    /// <summary>Base URL of the Bridge (e.g. http://127.0.0.1:43119).</summary>
    public required string BridgeUrl { get; init; }

    /// <summary>Bearer token for Bridge authentication.</summary>
    public string? AuthToken { get; init; }

    /// <summary>Action name (explain, review, propose).</summary>
    public required string Action { get; init; }

    /// <summary>Display name of the selected TIA object.</summary>
    public string ObjectName { get; init; } = "";

    /// <summary>Type of the selected TIA object.</summary>
    public string ObjectType { get; init; } = "";

    /// <summary>PLC name when available.</summary>
    public string? PlcName { get; init; }

    /// <summary>Project name when available.</summary>
    public string? ProjectName { get; init; }

    /// <summary>Correlation ID for tracing.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Pre-accepted status from the Bridge (if task was already accepted).</summary>
    public string? InitialStatus { get; init; }

    /// <summary>Stage from the Bridge (if task was already accepted).</summary>
    public string? InitialStage { get; init; }

    /// <summary>User-friendly action description.</summary>
    public string ActionDisplay => Action switch
    {
        "explain" => "Explain selected object",
        "review" => "Review selected object",
        "propose" => "Propose changes",
        _ => $"Action: {Action}"
    };
}
