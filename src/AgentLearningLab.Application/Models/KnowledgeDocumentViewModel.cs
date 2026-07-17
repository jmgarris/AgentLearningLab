namespace AgentLearningLab.Application.Models;

public sealed record KnowledgeDocumentViewModel(
    Guid DocumentId,
    string Title,
    string Summary,
    IReadOnlyList<KnowledgeChunkViewModel> Chunks);
