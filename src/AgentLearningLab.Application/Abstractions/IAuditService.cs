namespace AgentLearningLab.Application.Abstractions;

public interface IAuditService
{
    Task WriteAsync(
        string actorEmail,
        string actionType,
        string entityType,
        string entityId,
        string description,
        Guid? runId,
        Guid? approvalRequestId,
        string detailsJson,
        CancellationToken cancellationToken);
}
