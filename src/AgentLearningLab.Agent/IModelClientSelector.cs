using AgentLearningLab.Application.AI;

namespace AgentLearningLab.Agent;

public interface IModelClientSelector
{
    IModelClient GetCurrentClient();
}
