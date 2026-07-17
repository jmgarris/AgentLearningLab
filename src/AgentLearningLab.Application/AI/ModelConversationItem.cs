namespace AgentLearningLab.Application.AI;

public abstract record ModelConversationItem;

public sealed record ModelMessageItem(string Role, string Content) : ModelConversationItem;

public sealed record ModelToolResultItem(string CallId, string ToolName, string OutputJson) : ModelConversationItem;
