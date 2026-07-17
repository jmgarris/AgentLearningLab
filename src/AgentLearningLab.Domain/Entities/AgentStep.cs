using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class AgentStep : AuditableEntity
{
    public Guid AgentRunId { get; set; }

    public AgentRun? AgentRun { get; set; }

    public int StepNumber { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ErrorCode { get; set; }

    public ICollection<ToolExecution> ToolExecutions { get; set; } = [];
}
