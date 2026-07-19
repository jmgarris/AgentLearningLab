namespace AgentLearningLab.Application.Configuration;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public bool EnableDevelopmentResponseDiagnostics { get; set; }
}
