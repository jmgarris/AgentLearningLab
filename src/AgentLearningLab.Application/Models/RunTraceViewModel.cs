using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Models;

public sealed record RunTraceViewModel(
    Guid RunId,
    Guid ConversationId,
    string CorrelationId,
    string UserEmail,
    string ModelName,
    AgentRunStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<AgentStepSummary> Steps,
    ApprovalViewModel? Approval,
    AgentUsage? Usage,
    string? ErrorCode);
