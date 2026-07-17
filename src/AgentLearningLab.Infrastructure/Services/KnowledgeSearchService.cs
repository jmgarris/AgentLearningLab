using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class KnowledgeSearchService(AgentLearningLabDbContext dbContext) : IKnowledgeSearchService
{
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var queryTokens = Tokenize(query);

        var chunks = await dbContext.KnowledgeChunks
            .Include(x => x.KnowledgeDocument)
            .ToListAsync(cancellationToken);

        return chunks
            .Select(chunk =>
            {
                var contentTokens = Tokenize($"{chunk.Section} {chunk.Content}");
                var overlap = queryTokens.Intersect(contentTokens).Count();
                var exactPhraseBonus = chunk.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2.0 : 0.0;
                var score = queryTokens.Count == 0 ? 0.0 : (overlap / (double)queryTokens.Count) + exactPhraseBonus;

                return new KnowledgeSearchResult(
                    chunk.KnowledgeDocument?.Title ?? "Unknown",
                    chunk.Section,
                    chunk.Content,
                    score,
                    chunk.CitationId);
            })
            .Where(x => x.RelevanceScore > 0)
            .OrderByDescending(x => x.RelevanceScore)
            .ThenBy(x => x.CitationId)
            .Take(Math.Max(1, maximumResults))
            .ToList();
    }

    public async Task<IReadOnlyList<KnowledgeDocumentViewModel>> GetKnowledgeDocumentsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeDocuments
            .Include(x => x.Chunks)
            .OrderBy(x => x.Title)
            .Select(x => new KnowledgeDocumentViewModel(
                x.Id,
                x.Title,
                x.Summary,
                x.Chunks
                    .OrderBy(chunk => chunk.Sequence)
                    .Select(chunk => new KnowledgeChunkViewModel(
                        chunk.Id,
                        chunk.Section,
                        chunk.CitationId,
                        chunk.Content))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    private static HashSet<string> Tokenize(string input)
    {
        return input
            .Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '(', ')', '-', '—', '/', '§'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Where(x => x.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }
}
