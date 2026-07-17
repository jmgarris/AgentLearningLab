namespace AgentLearningLab.Application.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public AgentExecutionMode DefaultExecutionMode { get; set; } = AgentExecutionMode.Offline;

    public int MaximumSteps { get; set; } = 8;

    public int MaximumRecentMessages { get; set; } = 20;
}
