using AgentLearningLab.Application.Models;

namespace AgentLearningLab.Agent;

public sealed record AgentStepResult(
    int StepNumber,
    string Type,
    string Summary,
    string? ToolName,
    bool RequiresApproval,
    bool Success,
    string? ErrorCode = null,
    IReadOnlyList<AgentCitation>? Citations = null);
