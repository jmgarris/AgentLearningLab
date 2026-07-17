using AgentLearningLab.Application.Configuration;

namespace AgentLearningLab.Agent;

public interface IRuntimeModePreferenceStore
{
    ValueTask<AgentExecutionMode?> LoadAsync(CancellationToken cancellationToken);

    ValueTask SaveAsync(AgentExecutionMode mode, CancellationToken cancellationToken);
}
