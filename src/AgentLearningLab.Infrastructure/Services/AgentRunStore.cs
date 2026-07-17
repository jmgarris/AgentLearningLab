using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class AgentRunStore(AgentLearningLabDbContext dbContext) : IAgentRunStore
{
    public async Task<AgentRun> StartRunAsync(
        Guid conversationId,
        string correlationId,
        string userEmail,
        string modelName,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        var run = new AgentRun
        {
            ConversationId = conversationId,
            CorrelationId = correlationId,
            UserEmail = userEmail,
            ModelName = modelName,
            Status = AgentRunStatus.Failed,
            StartedAtUtc = startedAtUtc
        };

        dbContext.AgentRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        return run;
    }

    public async Task AddStepAsync(AgentStep step, CancellationToken cancellationToken)
    {
        dbContext.AgentSteps.Add(step);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddToolExecutionAsync(ToolExecution execution, CancellationToken cancellationToken)
    {
        dbContext.ToolExecutions.Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteRunAsync(
        Guid runId,
        AgentRunStatus status,
        DateTimeOffset completedAtUtc,
        int stepCount,
        AgentUsage? usage,
        string? errorCode,
        Guid? approvalRequestId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.AgentRuns.FirstAsync(x => x.Id == runId, cancellationToken);
        run.Status = status;
        run.CompletedAtUtc = completedAtUtc;
        run.StepCount = stepCount;
        run.ErrorCode = errorCode;
        run.ApprovalRequestId = approvalRequestId;
        run.PromptTokens = usage?.PromptTokens;
        run.CompletionTokens = usage?.CompletionTokens;
        run.TotalTokens = usage?.TotalTokens;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RunListItemViewModel>> ListRecentRunsAsync(CancellationToken cancellationToken)
    {
        var runs = await dbContext.AgentRuns
            .Include(x => x.Steps)
            .ToListAsync(cancellationToken);

        runs = runs
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(50)
            .ToList();

        var runIds = runs.Select(x => x.Id).ToArray();

        var toolExecutions = await dbContext.ToolExecutions
            .Where(x => runIds.Contains(x.AgentRunId))
            .ToListAsync(cancellationToken);

        var toolMap = toolExecutions
            .GroupBy(x => x.AgentRunId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(x => x.ToolName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList());

        var approvalMap = await dbContext.ApprovalRequests
            .Where(x => runIds.Contains(x.AgentRunId))
            .ToDictionaryAsync(x => x.AgentRunId, x => x.Status.ToString(), cancellationToken);

        return runs.Select(run =>
        {
            var duration = (run.CompletedAtUtc ?? run.StartedAtUtc) - run.StartedAtUtc;
            var tools = toolMap.GetValueOrDefault(run.Id) ?? [];

            return new RunListItemViewModel(
                run.Id,
                run.StartedAtUtc,
                run.UserEmail,
                run.Status,
                duration,
                run.StepCount,
                tools,
                approvalMap.GetValueOrDefault(run.Id),
                run.ErrorCode);
        }).ToList();
    }

    public async Task<RunTraceViewModel?> GetRunTraceAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.AgentRuns
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);

        if (run is null)
        {
            return null;
        }

        var toolExecutions = await dbContext.ToolExecutions
            .Where(x => x.AgentRunId == runId)
            .ToListAsync(cancellationToken);

        var approval = await dbContext.ApprovalRequests
            .Where(x => x.AgentRunId == runId)
            .Select(x => new ApprovalViewModel(
                x.Id,
                x.ToolName,
                x.ActionSummary,
                x.Status,
                x.ExpiresAtUtc,
                x.ValidatedArgumentsJson))
            .FirstOrDefaultAsync(cancellationToken);

        var stepSummaries = run.Steps
            .OrderBy(x => x.StepNumber)
            .Select(step =>
            {
                var tool = toolExecutions.FirstOrDefault(x => x.AgentStepId == step.Id);

                return new AgentStepSummary(
                    step.StepNumber,
                    step.StepType,
                    step.Summary,
                    tool?.ToolName,
                    tool?.RequiresApproval ?? false,
                    tool?.Success ?? true);
            })
            .ToList();

        AgentUsage? usage = null;
        if (run.CompletedAtUtc is not null)
        {
            usage = new AgentUsage(
                run.PromptTokens,
                run.CompletionTokens,
                run.TotalTokens,
                run.CompletedAtUtc.Value - run.StartedAtUtc);
        }

        return new RunTraceViewModel(
            run.Id,
            run.ConversationId,
            run.CorrelationId,
            run.UserEmail,
            run.ModelName,
            run.Status,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            stepSummaries,
            approval,
            usage,
            run.ErrorCode);
    }
}
