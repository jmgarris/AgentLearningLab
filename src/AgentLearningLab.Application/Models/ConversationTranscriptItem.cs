using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Models;

public sealed record ConversationTranscriptItem(
    Guid MessageId,
    AgentMessageKind Kind,
    string Sender,
    string Content,
    DateTimeOffset CreatedAtUtc,
    string? ToolName);
