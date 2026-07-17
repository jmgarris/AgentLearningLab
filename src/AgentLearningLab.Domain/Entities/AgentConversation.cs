using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class AgentConversation : AuditableEntity
{
    public string OwnerEmail { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public ICollection<AgentMessage> Messages { get; set; } = [];

    public ICollection<AgentRun> Runs { get; set; } = [];
}
