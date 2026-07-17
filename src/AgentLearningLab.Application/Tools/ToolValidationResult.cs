namespace AgentLearningLab.Application.Tools;

public sealed record ToolValidationResult(
    bool IsValid,
    string? NormalizedArgumentsJson,
    IReadOnlyList<string> Errors);
