namespace AgentLearningLab.Application.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaximumSteps { get; set; } = 8;

    public int MaximumRecentMessages { get; set; } = 20;
}
