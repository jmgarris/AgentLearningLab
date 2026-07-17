using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Models;

public sealed record ApprovalViewModel(
    Guid ApprovalRequestId,
    string ToolName,
    string ActionSummary,
    ApprovalStatus Status,
    DateTimeOffset ExpiresAtUtc,
    string ValidatedArgumentsJson);
