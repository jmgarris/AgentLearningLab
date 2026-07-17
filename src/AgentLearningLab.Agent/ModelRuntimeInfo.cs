namespace AgentLearningLab.Agent;

public sealed class ModelRuntimeInfo
{
    public ModelRuntimeInfo(AgentExecutionMode selectedMode, string activeModelName, bool apiKeyAvailable = true)
    {
        ActiveModelName = activeModelName;
        IsApiKeyAvailable = apiKeyAvailable;
        SelectedMode = selectedMode == AgentExecutionMode.ApiKey && !apiKeyAvailable
            ? AgentExecutionMode.Offline
            : selectedMode;
    }

    public ModelRuntimeInfo(bool defaultToOffline, string activeModelName, bool apiKeyAvailable = true)
        : this(
            defaultToOffline || !apiKeyAvailable
            ? AgentExecutionMode.Offline
            : AgentExecutionMode.ApiKey,
            activeModelName,
            apiKeyAvailable)
    {
    }

    public string ActiveModelName { get; }

    public bool IsApiKeyAvailable { get; }

    public AgentExecutionMode SelectedMode { get; private set; }

    public bool IsOffline => SelectedMode == AgentExecutionMode.Offline;

    public bool IsUsingApiKey => SelectedMode == AgentExecutionMode.ApiKey && IsApiKeyAvailable;

    public void UseOfflineMode()
    {
        SelectedMode = AgentExecutionMode.Offline;
    }

    public bool TryUseApiKeyMode()
    {
        if (!IsApiKeyAvailable)
        {
            return false;
        }

        SelectedMode = AgentExecutionMode.ApiKey;
        return true;
    }
}
