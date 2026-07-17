using AgentLearningLab.Application.Identity;

namespace AgentLearningLab.Application.Tools;

public sealed record ToolExecutionContext(
    Guid ConversationId,
    Guid RunId,
    string CorrelationId,
    AuthenticatedUserContext User,
    bool ApprovalGranted);
