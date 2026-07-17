using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class AuditEntry : AuditableEntity
{
    public Guid? AgentRunId { get; set; }

    public Guid? ApprovalRequestId { get; set; }

    public string ActorEmail { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DetailsJson { get; set; } = "{}";
}
