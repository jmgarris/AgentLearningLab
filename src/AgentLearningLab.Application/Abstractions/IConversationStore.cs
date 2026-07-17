using AgentLearningLab.Application.Identity;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Abstractions;

/// <summary>
/// Persists user-visible conversation state without storing hidden reasoning.
/// </summary>
public interface IConversationStore
{
    Task<AgentConversation> GetOrCreateConversationAsync(
        Guid? conversationId,
        AuthenticatedUserContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationSummaryViewModel>> ListConversationsAsync(
        string ownerEmail,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int maximumMessages,
        CancellationToken cancellationToken);

    Task AddMessageAsync(
        Guid conversationId,
        AgentMessageKind kind,
        string sender,
        string content,
        string? toolName,
        string? structuredDataJson,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationTranscriptItem>> GetTranscriptAsync(
        Guid conversationId,
        CancellationToken cancellationToken);

    Task ClearConversationAsync(Guid conversationId, CancellationToken cancellationToken);
}
