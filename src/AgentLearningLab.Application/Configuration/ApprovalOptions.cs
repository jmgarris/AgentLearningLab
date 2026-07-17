namespace AgentLearningLab.Application.Configuration;

public sealed class ApprovalOptions
{
    public const string SectionName = "Approval";

    public int ExpirationMinutes { get; set; } = 15;
}
