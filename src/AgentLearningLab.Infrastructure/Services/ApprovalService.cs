using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Common;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class ApprovalService(
    AgentLearningLabDbContext dbContext,
    ISystemClock clock,
    Microsoft.Extensions.Options.IOptions<AgentLearningLab.Application.Configuration.ApprovalOptions> options)
    : IApprovalService
{
    public async Task<ApprovalRequest> CreateAsync(
        Guid conversationId,
        Guid runId,
        string toolName,
        string toolCallId,
        string actionSummary,
        string validatedArgumentsJson,
        string requestingUserEmail,
        ClubRole requiredRole,
        string? modelResponseId,
        CancellationToken cancellationToken)
    {
        var request = new ApprovalRequest
        {
            ConversationId = conversationId,
            AgentRunId = runId,
            ToolName = toolName,
            ToolCallId = toolCallId,
            ActionSummary = actionSummary,
            ValidatedArgumentsJson = validatedArgumentsJson,
            RequestingUserEmail = requestingUserEmail,
            RequiredRole = requiredRole,
            ExpiresAtUtc = clock.UtcNow.AddMinutes(options.Value.ExpirationMinutes),
            Status = ApprovalStatus.Pending,
            ModelResponseId = modelResponseId
        };

        dbContext.ApprovalRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    public Task<ApprovalRequest?> GetAsync(Guid approvalRequestId, CancellationToken cancellationToken)
    {
        return dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == approvalRequestId, cancellationToken);
    }

    public Task<ApprovalViewModel?> GetViewModelAsync(Guid approvalRequestId, CancellationToken cancellationToken)
    {
        return dbContext.ApprovalRequests
            .Where(x => x.Id == approvalRequestId)
            .Select(x => new ApprovalViewModel(
                x.Id,
                x.ToolName,
                x.ActionSummary,
                x.Status,
                x.ExpiresAtUtc,
                x.ValidatedArgumentsJson))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ApprovalClaimResult> TryApproveAsync(
        Guid approvalRequestId,
        AuthenticatedUserContext decidingUser,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var approval = await dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == approvalRequestId, cancellationToken);

        if (approval is null)
        {
            return new ApprovalClaimResult(false, null, "approval_not_found", null);
        }

        if (approval.ExpiresAtUtc <= now && approval.Status == ApprovalStatus.Pending)
        {
            approval.Status = ApprovalStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ApprovalClaimResult(false, null, "approval_expired", approval);
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            var errorCode = approval.Status switch
            {
                ApprovalStatus.Executed => "approval_already_executed",
                ApprovalStatus.Rejected => "approval_rejected",
                ApprovalStatus.Expired => "approval_expired",
                ApprovalStatus.Failed => "approval_failed",
                _ => "approval_not_claimed"
            };

            return new ApprovalClaimResult(false, null, errorCode, approval);
        }

        if (approval.ExpiresAtUtc <= now)
        {
            approval.Status = ApprovalStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ApprovalClaimResult(false, null, "approval_expired", approval);
        }

        var executionToken = Guid.NewGuid().ToString("N");
        approval.Status = ApprovalStatus.Approved;
        approval.DecisionTimeUtc = now;
        approval.DecidingUserEmail = decidingUser.Email;
        approval.ExecutionToken = executionToken;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApprovalClaimResult(true, executionToken, null, approval);
    }

    public async Task RejectAsync(
        Guid approvalRequestId,
        AuthenticatedUserContext decidingUser,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var approval = await dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == approvalRequestId, cancellationToken);
        if (approval is null || approval.Status != ApprovalStatus.Pending)
        {
            return;
        }

        approval.Status = ApprovalStatus.Rejected;
        approval.DecisionTimeUtc = now;
        approval.DecidingUserEmail = decidingUser.Email;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkExecutedAsync(Guid approvalRequestId, string executionToken, CancellationToken cancellationToken)
    {
        var approval = await dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == approvalRequestId, cancellationToken);
        if (approval is null || approval.Status != ApprovalStatus.Approved || approval.ExecutionToken != executionToken)
        {
            return;
        }

        approval.Status = ApprovalStatus.Executed;
        approval.ExecutedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid approvalRequestId, string executionToken, CancellationToken cancellationToken)
    {
        var approval = await dbContext.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == approvalRequestId, cancellationToken);
        if (approval is null || approval.Status != ApprovalStatus.Approved || approval.ExecutionToken != executionToken)
        {
            return;
        }

        approval.Status = ApprovalStatus.Failed;
        approval.ExecutedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
