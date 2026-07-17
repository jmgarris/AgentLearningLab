namespace AgentLearningLab.Application.Configuration;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public int MaximumResults { get; set; } = 5;
}
