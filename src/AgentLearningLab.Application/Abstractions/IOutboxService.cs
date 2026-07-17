using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;

namespace AgentLearningLab.Application.Abstractions;

public interface IOutboxService
{
    Task<OutboxMessage> CreateDraftAsync(
        Guid recipientMemberId,
        string subject,
        string body,
        string createdByEmail,
        Guid? approvalRequestId,
        CancellationToken cancellationToken);

    Task MarkSentAsync(Guid outboxMessageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessageViewModel>> ListMessagesAsync(CancellationToken cancellationToken);
}
