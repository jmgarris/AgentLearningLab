using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Models;

public sealed record AgentRunResult(
    AgentRunStatus Status,
    string? FinalText,
    Guid ConversationId,
    Guid RunId,
    IReadOnlyList<AgentCitation> Citations,
    ApprovalViewModel? PendingApproval,
    IReadOnlyList<AgentStepSummary> Steps,
    AgentUsage? Usage,
    string? ErrorCode);
