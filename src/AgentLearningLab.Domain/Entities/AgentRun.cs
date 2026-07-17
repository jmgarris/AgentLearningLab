using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Domain.Entities;

public sealed class AgentRun : AuditableEntity
{
    public Guid ConversationId { get; set; }

    public AgentConversation? Conversation { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public AgentRunStatus Status { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int StepCount { get; set; }

    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    public int? TotalTokens { get; set; }

    public string? ErrorCode { get; set; }

    public Guid? ApprovalRequestId { get; set; }

    public ApprovalRequest? ApprovalRequest { get; set; }

    public ICollection<AgentStep> Steps { get; set; } = [];
}
