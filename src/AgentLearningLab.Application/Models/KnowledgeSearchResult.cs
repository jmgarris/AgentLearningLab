namespace AgentLearningLab.Application.Models;

public sealed record KnowledgeSearchResult(
    string DocumentTitle,
    string Section,
    string ChunkText,
    double RelevanceScore,
    string CitationId);
