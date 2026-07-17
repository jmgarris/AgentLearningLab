namespace AgentLearningLab.Application.Models;

public sealed record AgentUsage(
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    TimeSpan Duration);
