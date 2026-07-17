using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class ToolExecution : AuditableEntity
{
    public Guid AgentRunId { get; set; }

    public AgentRun? AgentRun { get; set; }

    public Guid? AgentStepId { get; set; }

    public AgentStep? AgentStep { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string ToolCallId { get; set; } = string.Empty;

    public string ValidatedArgumentsJson { get; set; } = "{}";

    public string ResultJson { get; set; } = "{}";

    public bool Success { get; set; }

    public bool RequiresApproval { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ErrorCode { get; set; }
}
