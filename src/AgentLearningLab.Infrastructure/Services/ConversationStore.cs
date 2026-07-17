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
                    x => x.Id == conversationId.Value
                        && x.OwnerEmail == user.Email
                        && !x.IsArchived,
                    cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        var conversation = new AgentConversation
        {
            OwnerEmail = user.Email,
            Title = $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            IsArchived = false
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
            .Where(x => x.OwnerEmail == ownerEmail && !x.IsArchived)
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
        var newestFirst = await dbContext.AgentMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SequenceNumber)
            .Take(maximumMessages)
            .ToListAsync(cancellationToken);

        newestFirst.Reverse();
        return newestFirst;
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
        var nextSequenceNumber = (await dbContext.AgentMessages
            .Where(x => x.ConversationId == conversationId)
            .MaxAsync(x => (long?)x.SequenceNumber, cancellationToken) ?? 0) + 1;

        dbContext.AgentMessages.Add(new AgentMessage
        {
            ConversationId = conversationId,
            SequenceNumber = nextSequenceNumber,
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
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new ConversationTranscriptItem(
                x.Id,
                x.Kind,
                x.Sender,
                x.Content,
                x.CreatedAtUtc,
                x.ToolName))
            .ToListAsync(cancellationToken);

        return transcript;
    }

    public async Task ArchiveConversationAsync(
        Guid conversationId,
        AuthenticatedUserContext user,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.AgentConversations
            .FirstOrDefaultAsync(
                x => x.Id == conversationId && x.OwnerEmail == user.Email,
                cancellationToken);

        if (conversation is null)
        {
            throw new InvalidOperationException("Conversation not found for the current user.");
        }

        conversation.IsArchived = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
