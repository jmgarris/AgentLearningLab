namespace AgentLearningLab.Application.Configuration;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string Model { get; set; } = "gpt-5.6-terra";

    public int TimeoutSeconds { get; set; } = 60;
}
