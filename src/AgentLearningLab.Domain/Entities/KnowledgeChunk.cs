using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class KnowledgeChunk : AuditableEntity
{
    public Guid KnowledgeDocumentId { get; set; }

    public KnowledgeDocument? KnowledgeDocument { get; set; }

    public int Sequence { get; set; }

    public string Section { get; set; } = string.Empty;

    public string CitationId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
