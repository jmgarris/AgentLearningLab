namespace AgentLearningLab.Application.Models;

public sealed record ConversationSummaryViewModel(
    Guid ConversationId,
    string Title,
    DateTimeOffset UpdatedAtUtc);
