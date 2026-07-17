using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Models;

public sealed record OutboxMessageViewModel(
    Guid MessageId,
    string RecipientName,
    string RecipientEmail,
    string Subject,
    string Body,
    OutboxStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc);
