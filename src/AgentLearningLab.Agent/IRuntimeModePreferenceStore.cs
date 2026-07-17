namespace AgentLearningLab.Agent;

public interface IRuntimeModePreferenceStore
{
    AgentExecutionMode GetPreferredMode(bool apiKeyAvailable);

    void Save(AgentExecutionMode mode);
}
