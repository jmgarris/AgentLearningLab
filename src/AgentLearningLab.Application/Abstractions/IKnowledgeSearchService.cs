using AgentLearningLab.Application.Models;

namespace AgentLearningLab.Application.Abstractions;

public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeDocumentViewModel>> GetKnowledgeDocumentsAsync(CancellationToken cancellationToken);
}
