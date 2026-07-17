namespace AgentLearningLab.Domain.Enums;

public enum AgentRunStatus
{
    Completed = 1,
    AwaitingApproval = 2,
    Rejected = 3,
    Failed = 4,
    MaximumStepsExceeded = 5
}
