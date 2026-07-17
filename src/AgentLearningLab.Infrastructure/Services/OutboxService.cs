using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class OutboxService(AgentLearningLabDbContext dbContext) : IOutboxService
{
    public async Task<OutboxMessage> CreateDraftAsync(
        Guid recipientMemberId,
        string subject,
        string body,
        string createdByEmail,
        Guid? approvalRequestId,
        CancellationToken cancellationToken)
    {
        var recipient = await dbContext.ClubMembers.FirstAsync(x => x.Id == recipientMemberId, cancellationToken);

        var message = new OutboxMessage
        {
            RecipientMemberId = recipientMemberId,
            ApprovalRequestId = approvalRequestId,
            RecipientName = recipient.DisplayName,
            RecipientEmail = recipient.Email,
            Subject = subject,
            Body = body,
            Status = OutboxStatus.Draft,
            CreatedByEmail = createdByEmail
        };

        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    public async Task MarkSentAsync(Guid outboxMessageId, CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages.FirstAsync(x => x.Id == outboxMessageId, cancellationToken);
        message.Status = OutboxStatus.Sent;
        message.SentAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<OutboxMessageViewModel>> ListMessagesAsync(CancellationToken cancellationToken)
    {
        return dbContext.OutboxMessages
            .Select(x => new OutboxMessageViewModel(
                x.Id,
                x.RecipientName,
                x.RecipientEmail,
                x.Subject,
                x.Body,
                x.Status,
                x.CreatedAtUtc,
                x.SentAtUtc))
            .ToListAsync(cancellationToken)
            .ContinueWith(
                static task => (IReadOnlyList<OutboxMessageViewModel>)task.Result
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ToList(),
                cancellationToken);
    }
}
