using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class ConversationStore(AgentLearningLabDbContext dbContext) : IConversationStore
{
    public async Task<AgentConversation> GetOrCreateConversationAsync(
        Guid? conversationId,
        AuthenticatedUserContext user,
        CancellationToken cancellationToken)
    {
        if (conversationId.HasValue)
        {
            var existing = await dbContext.AgentConversations
                .FirstOrDefaultAsync(
                    x => x.Id == conversationId.Value && x.OwnerEmail == user.Email,
                    cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        var conversation = new AgentConversation
        {
            OwnerEmail = user.Email,
            Title = $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
        };

        dbContext.AgentConversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return conversation;
    }

    public async Task<IReadOnlyList<ConversationSummaryViewModel>> ListConversationsAsync(
        string ownerEmail,
        CancellationToken cancellationToken)
    {
        var conversations = await dbContext.AgentConversations
            .Where(x => x.OwnerEmail == ownerEmail)
            .Select(x => new ConversationSummaryViewModel(x.Id, x.Title, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return conversations
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<AgentMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        // SQLite does not natively sort DateTimeOffset values server-side in a way EF can translate
        // reliably for this query, so we load the conversation messages and apply the recency window
        // in memory. The configured conversation window is intentionally small for this sample.
        var messages = await dbContext.AgentMessages
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return messages
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(maximumMessages)
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();
    }

    public async Task AddMessageAsync(
        Guid conversationId,
        AgentMessageKind kind,
        string sender,
        string content,
        string? toolName,
        string? structuredDataJson,
        CancellationToken cancellationToken)
    {
        dbContext.AgentMessages.Add(new AgentMessage
        {
            ConversationId = conversationId,
            Kind = kind,
            Sender = sender,
            Content = content,
            ToolName = toolName,
            StructuredDataJson = structuredDataJson
        });

        var conversation = await dbContext.AgentConversations.FirstAsync(x => x.Id == conversationId, cancellationToken);

        if (kind == AgentMessageKind.User && string.IsNullOrWhiteSpace(conversation.Title))
        {
            conversation.Title = content.Length > 50 ? $"{content[..50]}..." : content;
        }

        conversation.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationTranscriptItem>> GetTranscriptAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var transcript = await dbContext.AgentMessages
            .Where(x => x.ConversationId == conversationId)
            .Select(x => new ConversationTranscriptItem(
                x.Id,
                x.Kind,
                x.Sender,
                x.Content,
                x.CreatedAtUtc,
                x.ToolName))
            .ToListAsync(cancellationToken);

        return transcript
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();
    }

    public async Task ClearConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var messages = await dbContext.AgentMessages
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        dbContext.AgentMessages.RemoveRange(messages);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
