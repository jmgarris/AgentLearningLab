using AgentLearningLab.Application.Models;

namespace AgentLearningLab.Application.Tools;

public sealed record ToolExecutionResult(
    bool Success,
    string ResultJson,
    string Summary,
    IReadOnlyList<AgentCitation> Citations,
    string? ErrorCode = null);
