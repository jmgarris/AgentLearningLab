namespace AgentLearningLab.Application.Models;

public sealed record AgentStepSummary(
    int StepNumber,
    string Type,
    string Summary,
    string? ToolName,
    bool RequiresApproval,
    bool Success);
