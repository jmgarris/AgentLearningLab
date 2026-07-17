namespace AgentLearningLab.Application.Models;

public sealed record KnowledgeChunkViewModel(
    Guid ChunkId,
    string Section,
    string CitationId,
    string Content);
