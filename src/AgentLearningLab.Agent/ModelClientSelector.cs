using AgentLearningLab.Application.AI;

namespace AgentLearningLab.Agent;

public sealed class ModelClientSelector(
    ModelRuntimeInfo runtimeInfo,
    IOfflineModelClient fakeModelClient,
    IApiModelClient openAiResponsesModelClient)
    : IModelClientSelector
{
    public IModelClient GetCurrentClient()
    {
        return runtimeInfo.IsUsingApiKey
            ? openAiResponsesModelClient
            : fakeModelClient;
    }
}
