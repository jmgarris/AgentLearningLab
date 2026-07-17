using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Domain.Entities;

public sealed class AgentMessage : AuditableEntity
{
    public Guid ConversationId { get; set; }

    public AgentConversation? Conversation { get; set; }

    public AgentMessageKind Kind { get; set; }

    public string Sender { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ToolName { get; set; }

    public string? StructuredDataJson { get; set; }
}
