using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class KnowledgeDocument : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string SourceCode { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ICollection<KnowledgeChunk> Chunks { get; set; } = [];
}
