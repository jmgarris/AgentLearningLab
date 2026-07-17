using AgentLearningLab.Application.Configuration;

namespace AgentLearningLab.Agent;

public sealed class NoOpRuntimeModePreferenceStore : IRuntimeModePreferenceStore
{
    public ValueTask<AgentExecutionMode?> LoadAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<AgentExecutionMode?>(null);

    public ValueTask SaveAsync(AgentExecutionMode mode, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
