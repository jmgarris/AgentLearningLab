namespace AgentLearningLab.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
