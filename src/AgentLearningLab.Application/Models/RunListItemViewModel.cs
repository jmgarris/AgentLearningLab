using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Models;

public sealed record RunListItemViewModel(
    Guid RunId,
    DateTimeOffset StartedAtUtc,
    string UserEmail,
    AgentRunStatus Status,
    TimeSpan Duration,
    int StepCount,
    IReadOnlyList<string> ToolsUsed,
    string? ApprovalStatus,
    string? ErrorCode);
