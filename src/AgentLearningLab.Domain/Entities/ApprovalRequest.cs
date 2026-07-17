using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Domain.Entities;

public sealed class ApprovalRequest : AuditableEntity
{
    public Guid ConversationId { get; set; }

    public Guid AgentRunId { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string ToolCallId { get; set; } = string.Empty;

    public string ActionSummary { get; set; } = string.Empty;

    public string ValidatedArgumentsJson { get; set; } = "{}";

    public string RequestingUserEmail { get; set; } = string.Empty;

    public ClubRole RequiredRole { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public ApprovalStatus Status { get; set; }

    public DateTimeOffset? DecisionTimeUtc { get; set; }

    public string? DecidingUserEmail { get; set; }

    public DateTimeOffset? ExecutedAtUtc { get; set; }

    public string? ExecutionToken { get; set; }

    public string? ModelResponseId { get; set; }
}
