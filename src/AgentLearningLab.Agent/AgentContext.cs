using AgentLearningLab.Application.Identity;

namespace AgentLearningLab.Agent;

public sealed record AgentContext(
    Guid ConversationId,
    Guid RunId,
    string CorrelationId,
    AuthenticatedUserContext User);
