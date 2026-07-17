using AgentLearningLab.Application.Models;

namespace AgentLearningLab.Application.AI;

public sealed record ModelTurnResult(
    string? ResponseId,
    string? FinalText,
    IReadOnlyList<ModelToolCall> ToolCalls,
    AgentUsage? Usage);
