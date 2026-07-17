using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Infrastructure.Persistence;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class AuditService(AgentLearningLabDbContext dbContext) : IAuditService
{
    public async Task WriteAsync(
        string actorEmail,
        string actionType,
        string entityType,
        string entityId,
        string description,
        Guid? runId,
        Guid? approvalRequestId,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        dbContext.AuditEntries.Add(new AuditEntry
        {
            ActorEmail = actorEmail,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            AgentRunId = runId,
            ApprovalRequestId = approvalRequestId,
            DetailsJson = detailsJson
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
