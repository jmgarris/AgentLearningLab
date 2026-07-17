using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class UserMemoryFact : AuditableEntity
{
    public string OwnerEmail { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string CreatedByEmail { get; set; } = string.Empty;
}
