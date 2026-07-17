namespace AgentLearningLab.Application.AI;

public sealed record ModelTurnRequest(
    string Model,
    string Instructions,
    IReadOnlyList<ModelConversationItem> InputItems,
    IReadOnlyList<ModelToolDefinition> Tools,
    string? PreviousResponseId = null);
