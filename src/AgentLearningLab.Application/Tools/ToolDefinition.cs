using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Tools;

public sealed record ToolDefinition(
    string Name,
    string Description,
    string JsonSchema,
    ToolAccessMode AccessMode,
    bool RequiresApproval,
    ClubRole MinimumRole);
