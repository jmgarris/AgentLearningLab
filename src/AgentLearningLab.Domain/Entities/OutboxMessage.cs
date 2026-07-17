using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Domain.Entities;

public sealed class OutboxMessage : AuditableEntity
{
    public Guid RecipientMemberId { get; set; }

    public ClubMember? RecipientMember { get; set; }

    public Guid? ApprovalRequestId { get; set; }

    public ApprovalRequest? ApprovalRequest { get; set; }

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public OutboxStatus Status { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }

    public string CreatedByEmail { get; set; } = string.Empty;
}
