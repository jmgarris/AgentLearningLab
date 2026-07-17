namespace AgentLearningLab.Application.AI;

public sealed record ModelToolCall(
    string CallId,
    string ToolName,
    string ArgumentsJson);
