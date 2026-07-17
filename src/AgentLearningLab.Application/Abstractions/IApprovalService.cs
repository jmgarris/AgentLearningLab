using AgentLearningLab.Application.Identity;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Abstractions;

public interface IApprovalService
{
    Task<ApprovalRequest> CreateAsync(
        Guid conversationId,
        Guid runId,
        string toolName,
        string toolCallId,
        string actionSummary,
        string validatedArgumentsJson,
        string requestingUserEmail,
        ClubRole requiredRole,
        string? modelResponseId,
        CancellationToken cancellationToken);

    Task<ApprovalRequest?> GetAsync(Guid approvalRequestId, CancellationToken cancellationToken);

    Task<ApprovalViewModel?> GetViewModelAsync(Guid approvalRequestId, CancellationToken cancellationToken);

    Task<ApprovalClaimResult> TryApproveAsync(
        Guid approvalRequestId,
        AuthenticatedUserContext decidingUser,
        CancellationToken cancellationToken);

    Task RejectAsync(
        Guid approvalRequestId,
        AuthenticatedUserContext decidingUser,
        CancellationToken cancellationToken);

    Task MarkExecutedAsync(
        Guid approvalRequestId,
        string executionToken,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid approvalRequestId,
        string executionToken,
        CancellationToken cancellationToken);
}
