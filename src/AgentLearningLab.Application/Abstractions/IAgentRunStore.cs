using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Abstractions;

public interface IAgentRunStore
{
    Task<AgentRun> StartRunAsync(
        Guid conversationId,
        string correlationId,
        string userEmail,
        string modelName,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken);

    Task AddStepAsync(AgentStep step, CancellationToken cancellationToken);

    Task AddToolExecutionAsync(ToolExecution execution, CancellationToken cancellationToken);

    Task CompleteRunAsync(
        Guid runId,
        AgentRunStatus status,
        DateTimeOffset completedAtUtc,
        int stepCount,
        AgentUsage? usage,
        string? errorCode,
        Guid? approvalRequestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RunListItemViewModel>> ListRecentRunsAsync(CancellationToken cancellationToken);

    Task<RunTraceViewModel?> GetRunTraceAsync(Guid runId, CancellationToken cancellationToken);
}
