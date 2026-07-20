using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.Application.Audit;

/// <summary>
/// Structured audit writer that logs audit events via ILogger.
/// Does not log secrets, API keys, or full PLC source code.
/// </summary>
public class AuditWriter : IAuditWriter
{
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(ILogger<AuditWriter> logger)
    {
        _logger = logger;
    }

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Audit: {EventType} correlationId={CorrelationId} tiaSession={TiaSession} project={Project} " +
            "tool={Tool} object={ObjectId} success={Success} details={Details} ts={Timestamp}",
            auditEvent.EventType,
            auditEvent.CorrelationId,
            auditEvent.TiaSessionId,
            auditEvent.ProjectId,
            auditEvent.ToolName,
            auditEvent.ObjectId,
            auditEvent.Success,
            auditEvent.Details,
            auditEvent.Timestamp);

        return Task.CompletedTask;
    }
}
