namespace AgentLearningLab.Agent;

public static class PromptLoader
{
    public static AgentDefinition LoadClubOpsDefinition()
    {
        const string resourceName = "AgentLearningLab.Agent.Prompts.club-ops-agent.md";
        using var stream = typeof(PromptLoader).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded prompt resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        var instructions = reader.ReadToEnd();
        return new AgentDefinition("ClubOps Learning Agent", instructions);
    }
}
