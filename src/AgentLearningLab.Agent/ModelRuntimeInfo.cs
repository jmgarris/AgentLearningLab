using AgentLearningLab.Application.Configuration;

namespace AgentLearningLab.Agent;

public sealed class ModelRuntimeInfo
{
    public ModelRuntimeInfo(
        AgentExecutionMode defaultExecutionMode,
        string activeModelName,
        bool apiKeyAvailable)
    {
        ActiveModelName = activeModelName.Trim();
        IsApiKeyAvailable = apiKeyAvailable;
        SelectedMode = AgentExecutionMode.Offline;

        TrySetMode(defaultExecutionMode);
    }

    public string ActiveModelName { get; }

    public bool IsApiKeyAvailable { get; }

    public bool IsModelConfigured => !string.IsNullOrWhiteSpace(ActiveModelName);

    public bool IsApiModeAvailable => IsApiKeyAvailable && IsModelConfigured;

    public string? ApiModeUnavailableReason
    {
        get
        {
            if (!IsApiKeyAvailable)
            {
                return "OPENAI_API_KEY is not set for this app session.";
            }

            if (!IsModelConfigured)
            {
                return "No OpenAI model is configured. Set OpenAI:Model or OpenAI__Model before using API key mode.";
            }

            return null;
        }
    }

    public AgentExecutionMode SelectedMode { get; private set; }

    public bool IsOffline => SelectedMode == AgentExecutionMode.Offline;

    public bool IsUsingApiKey => SelectedMode == AgentExecutionMode.ApiKey && IsApiModeAvailable;

    public string CurrentModelLabel => IsOffline ? "offline-fake-model" : ActiveModelName;

    public event EventHandler? Changed;

    public bool TrySetMode(AgentExecutionMode mode)
    {
        var effectiveMode = mode == AgentExecutionMode.ApiKey && !IsApiModeAvailable
            ? AgentExecutionMode.Offline
            : mode;

        if (mode == AgentExecutionMode.ApiKey && !IsApiModeAvailable)
        {
            return false;
        }

        if (SelectedMode == effectiveMode)
        {
            return true;
        }

        SelectedMode = effectiveMode;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool ApplySavedPreference(AgentExecutionMode? preferredMode)
    {
        if (preferredMode is null)
        {
            return false;
        }

        return TrySetMode(preferredMode.Value);
    }
}
