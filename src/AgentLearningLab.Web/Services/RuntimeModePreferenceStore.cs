using AgentLearningLab.Agent;
using AgentLearningLab.Application.Configuration;
using Microsoft.JSInterop;

namespace AgentLearningLab.Web.Services;

public sealed class RuntimeModePreferenceStore(IJSRuntime jsRuntime) : IRuntimeModePreferenceStore
{
    private const string StorageKey = "agent-learning-lab.runtime-mode";

    public async ValueTask<AgentExecutionMode?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storedValue = await jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                StorageKey);

            return storedValue?.ToLowerInvariant() switch
            {
                "offline" => AgentExecutionMode.Offline,
                "api-key" => AgentExecutionMode.ApiKey,
                _ => null
            };
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async ValueTask SaveAsync(AgentExecutionMode mode, CancellationToken cancellationToken)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                StorageKey,
                mode == AgentExecutionMode.ApiKey ? "api-key" : "offline");
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
