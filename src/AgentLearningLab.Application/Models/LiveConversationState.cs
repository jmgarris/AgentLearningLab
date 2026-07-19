namespace AgentLearningLab.Application.Models;

public sealed record LiveConversationState(
    string ResponseId,
    string Model,
    DateTimeOffset UpdatedAtUtc);
