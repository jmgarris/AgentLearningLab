using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Authorization;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AgentLearningLab.Agent;

public sealed class AgentRunner
{
    private readonly ClubOpsAgent _agent;
    private readonly AgentOptions _agentOptions;
    private readonly IApprovalService _approvalService;
    private readonly IConversationStore _conversationStore;
    private readonly IAgentRunStore _runStore;
    private readonly IModelClientSelector _modelClientSelector;
    private readonly ModelRuntimeInfo _runtimeInfo;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILogger<AgentRunner> _logger;

    public AgentRunner(
        ClubOpsAgent agent,
        IConversationStore conversationStore,
        IAgentRunStore runStore,
        IModelClientSelector modelClientSelector,
        ToolRegistry toolRegistry,
        IApprovalService approvalService,
        IOptions<AgentOptions> agentOptions,
        ModelRuntimeInfo runtimeInfo,
        ILogger<AgentRunner> logger)
    {
        _agent = agent;
        _conversationStore = conversationStore;
        _runStore = runStore;
        _modelClientSelector = modelClientSelector;
        _toolRegistry = toolRegistry;
        _approvalService = approvalService;
        _agentOptions = agentOptions.Value;
        _runtimeInfo = runtimeInfo;
        _logger = logger;
    }

    public bool IsOfflineMode => _runtimeInfo.IsOffline;

    public async Task<AgentRunResult> RunAsync(
        Guid? conversationId,
        string userMessage,
        AuthenticatedUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        var normalizedUserMessage = userMessage.Trim();

        var conversation = await _conversationStore.GetOrCreateConversationAsync(conversationId, user, cancellationToken);
        if (!_runtimeInfo.IsUsingApiKey)
        {
            await _conversationStore.ResetLiveConversationStateAsync(conversation.Id, user, cancellationToken);
        }

        await _conversationStore.AddMessageAsync(
            conversation.Id,
            AgentMessageKind.User,
            user.Email,
            normalizedUserMessage,
            null,
            null,
            cancellationToken);

        string? previousResponseId = null;
        IReadOnlyList<ModelConversationItem>? initialInputItems = null;
        if (_runtimeInfo.IsUsingApiKey)
        {
            var liveState = await _conversationStore.GetLiveConversationStateAsync(conversation.Id, user, cancellationToken);
            if (liveState is not null)
            {
                if (string.Equals(liveState.Model, _runtimeInfo.CurrentModelLabel, StringComparison.Ordinal))
                {
                    previousResponseId = liveState.ResponseId;
                    initialInputItems = [new ModelMessageItem("user", normalizedUserMessage)];
                }
                else
                {
                    await _conversationStore.ResetLiveConversationStateAsync(conversation.Id, user, cancellationToken);
                }
            }
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var run = await _runStore.StartRunAsync(
            conversation.Id,
            correlationId,
            user.Email,
            _runtimeInfo.CurrentModelLabel,
            DateTimeOffset.UtcNow,
            cancellationToken);

        var context = new AgentContext(conversation.Id, run.Id, correlationId, user);
        return await ContinueLoopAsync(
            context,
            user,
            0,
            previousResponseId,
            initialInputItems,
            cancellationToken);
    }

    public async Task<AgentRunResult> ApproveAsync(
        Guid approvalRequestId,
        AuthenticatedUserContext decidingUser,
        CancellationToken cancellationToken)
    {
        var claim = await _approvalService.TryApproveAsync(approvalRequestId, decidingUser, cancellationToken);
        if (!claim.Success || claim.ApprovalRequest is null || string.IsNullOrWhiteSpace(claim.ExecutionToken))
        {
            return new AgentRunResult(
                AgentRunStatus.Failed,
                "The approval could not be processed.",
                claim.ApprovalRequest?.ConversationId ?? Guid.Empty,
                claim.ApprovalRequest?.AgentRunId ?? Guid.Empty,
                [],
                claim.ApprovalRequest is null ? null : await _approvalService.GetViewModelAsync(approvalRequestId, cancellationToken),
                [],
                null,
                claim.ErrorCode);
        }

        if (!RoleHelpers.MeetsMinimumRole(decidingUser.Role, claim.ApprovalRequest.RequiredRole))
        {
            return new AgentRunResult(
                AgentRunStatus.Failed,
                "You are not authorized to approve this action.",
                claim.ApprovalRequest.ConversationId,
                claim.ApprovalRequest.AgentRunId,
                [],
                await _approvalService.GetViewModelAsync(approvalRequestId, cancellationToken),
                [],
                null,
                "approval_unauthorized");
        }

        if (!_toolRegistry.TryGet(claim.ApprovalRequest.ToolName, out var tool) || tool is null)
        {
            return new AgentRunResult(
                AgentRunStatus.Failed,
                "The approved tool could not be found.",
                claim.ApprovalRequest.ConversationId,
                claim.ApprovalRequest.AgentRunId,
                [],
                await _approvalService.GetViewModelAsync(approvalRequestId, cancellationToken),
                [],
                null,
                "approval_tool_not_found");
        }

        var validation = tool.Validate(claim.ApprovalRequest.ValidatedArgumentsJson);
        if (!validation.IsValid || validation.NormalizedArgumentsJson is null)
        {
            return new AgentRunResult(
                AgentRunStatus.Failed,
                "Stored approval arguments are no longer valid.",
                claim.ApprovalRequest.ConversationId,
                claim.ApprovalRequest.AgentRunId,
                [],
                await _approvalService.GetViewModelAsync(approvalRequestId, cancellationToken),
                [],
                null,
                "approval_arguments_invalid");
        }

        using var arguments = JsonDocument.Parse(validation.NormalizedArgumentsJson);
        var existingTrace = await _runStore.GetRunTraceAsync(claim.ApprovalRequest.AgentRunId, cancellationToken);
        var existingStepCount = existingTrace?.Steps.Count ?? 0;
        var conversationOwner = CreateConversationOwnerContext(
            claim.ApprovalRequest.RequestingUserEmail,
            claim.ApprovalRequest.RequiredRole);
        var context = new AgentContext(
            claim.ApprovalRequest.ConversationId,
            claim.ApprovalRequest.AgentRunId,
            Guid.NewGuid().ToString("N"),
            decidingUser);

        try
        {
            var execution = await ExecuteToolAsync(
                context,
                existingStepCount + 1,
                tool,
                claim.ApprovalRequest.ToolCallId,
                arguments,
                approvalGranted: true,
                cancellationToken);

            await _approvalService.MarkExecutedAsync(approvalRequestId, claim.ExecutionToken, cancellationToken);

            if (execution.StructuredDataJson is not null)
            {
                await _conversationStore.AddMessageAsync(
                    context.ConversationId,
                    AgentMessageKind.Tool,
                    tool.Definition.Name,
                    execution.Summary,
                    tool.Definition.Name,
                    execution.StructuredDataJson,
                    cancellationToken);
            }

            return await ContinueLoopAsync(
                context,
                conversationOwner,
                existingStepCount + 1,
                claim.ApprovalRequest.ModelResponseId,
                [new ModelToolResultItem(claim.ApprovalRequest.ToolCallId, tool.Definition.Name, execution.OutputJson)],
                cancellationToken);
        }
        catch
        {
            await _approvalService.MarkFailedAsync(approvalRequestId, claim.ExecutionToken, cancellationToken);
            throw;
        }
    }

    public async Task<AgentRunResult> RejectAsync(
        Guid approvalRequestId,
        AuthenticatedUserContext decidingUser,
        CancellationToken cancellationToken)
    {
        var approval = await _approvalService.GetAsync(approvalRequestId, cancellationToken);
        if (approval is null)
        {
            return new AgentRunResult(AgentRunStatus.Failed, "Approval request not found.", Guid.Empty, Guid.Empty, [], null, [], null, "approval_not_found");
        }

        if (!RoleHelpers.MeetsMinimumRole(decidingUser.Role, approval.RequiredRole))
        {
            return new AgentRunResult(AgentRunStatus.Failed, "You are not authorized to reject this action.", approval.ConversationId, approval.AgentRunId, [], null, [], null, "approval_unauthorized");
        }

        await _approvalService.RejectAsync(approvalRequestId, decidingUser, cancellationToken);
        await _conversationStore.AddMessageAsync(
            approval.ConversationId,
            AgentMessageKind.Assistant,
            _agent.Definition.Name,
            "Approval was rejected. No side effect was executed.",
            null,
            null,
            cancellationToken);
        await _conversationStore.ResetLiveConversationStateAsync(
            approval.ConversationId,
            CreateConversationOwnerContext(approval.RequestingUserEmail, approval.RequiredRole),
            cancellationToken);

        await _runStore.CompleteRunAsync(
            approval.AgentRunId,
            AgentRunStatus.Rejected,
            DateTimeOffset.UtcNow,
            (await _runStore.GetRunTraceAsync(approval.AgentRunId, cancellationToken))?.Steps.Count ?? 0,
            null,
            null,
            approvalRequestId,
            cancellationToken);

        return new AgentRunResult(
            AgentRunStatus.Rejected,
            "Approval was rejected. No side effect was executed.",
            approval.ConversationId,
            approval.AgentRunId,
            [],
            await _approvalService.GetViewModelAsync(approvalRequestId, cancellationToken),
            [],
            null,
            null);
    }

    private async Task<AgentRunResult> ContinueLoopAsync(
        AgentContext context,
        AuthenticatedUserContext conversationOwner,
        int existingStepCount,
        string? previousResponseId,
        IReadOnlyList<ModelConversationItem>? continuationItems,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.CorrelationId,
            ["ConversationId"] = context.ConversationId,
            ["RunId"] = context.RunId,
            ["UserEmail"] = context.User.Email
        });

        var seenToolCalls = new HashSet<string>(StringComparer.Ordinal);
        var citations = new Dictionary<string, AgentCitation>(StringComparer.Ordinal);
        var stepSummaries = new List<AgentStepSummary>();
        var recoveredInvalidPreviousResponse = false;

        var existingTrace = await _runStore.GetRunTraceAsync(context.RunId, cancellationToken);
        if (existingTrace is not null)
        {
            stepSummaries.AddRange(existingTrace.Steps);
        }

        for (var stepNumber = existingStepCount + 1; stepNumber <= _agentOptions.MaximumSteps; stepNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAtUtc = DateTimeOffset.UtcNow;
            var modelClient = _modelClientSelector.GetCurrentClient();
            ModelTurnResult modelResult;

            while (true)
            {
                IReadOnlyList<ModelConversationItem> inputItems;
                if (previousResponseId is not null && continuationItems is not null)
                {
                    inputItems = continuationItems;
                }
                else
                {
                    var recentMessages = await _conversationStore.GetRecentMessagesAsync(
                        context.ConversationId,
                        _agentOptions.MaximumRecentMessages,
                        cancellationToken);
                    inputItems = BuildSafeTranscriptInputItems(recentMessages);
                }

                var request = new ModelTurnRequest(
                    _runtimeInfo.CurrentModelLabel,
                    _agent.Definition.Instructions,
                    inputItems,
                    _toolRegistry.GetModelToolDefinitions(),
                    previousResponseId);

                _logger.LogInformation("Starting agent step {StepNumber}", stepNumber);

                try
                {
                    modelResult = await modelClient.CreateTurnAsync(request, cancellationToken);
                    break;
                }
                catch (OpenAIRequestException ex) when (
                    _runtimeInfo.IsUsingApiKey &&
                    !recoveredInvalidPreviousResponse &&
                    ex.ErrorCode == "openai_previous_response_invalid" &&
                    !string.IsNullOrWhiteSpace(previousResponseId))
                {
                    _logger.LogWarning(
                        ex,
                        "Stored previous_response_id {PreviousResponseId} is no longer valid for conversation {ConversationId}. Retrying with rebuilt transcript.",
                        previousResponseId,
                        context.ConversationId);

                    await _conversationStore.ResetLiveConversationStateAsync(
                        context.ConversationId,
                        conversationOwner,
                        cancellationToken);

                    previousResponseId = null;
                    continuationItems = null;
                    recoveredInvalidPreviousResponse = true;
                }
            }

            previousResponseId = modelResult.ResponseId;
            continuationItems = null;
            if (_runtimeInfo.IsUsingApiKey && !string.IsNullOrWhiteSpace(modelResult.ResponseId))
            {
                await _conversationStore.SaveLiveConversationStateAsync(
                    context.ConversationId,
                    conversationOwner,
                    modelResult.ResponseId,
                    _runtimeInfo.CurrentModelLabel,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }

            if (modelResult.ToolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(modelResult.FinalText))
                {
                    return await FailAsync(context, stepSummaries, "Model returned neither tool calls nor final text.", "empty_model_result", cancellationToken);
                }

                await _conversationStore.AddMessageAsync(
                    context.ConversationId,
                    AgentMessageKind.Assistant,
                    _agent.Definition.Name,
                    modelResult.FinalText,
                    null,
                    null,
                    cancellationToken);

                var summary = new AgentStepSummary(stepNumber, "FinalResponse", "Returned final response.", null, false, true);
                stepSummaries.Add(summary);
                await _runStore.AddStepAsync(new AgentStep
                {
                    AgentRunId = context.RunId,
                    StepNumber = stepNumber,
                    StepType = "FinalResponse",
                    Summary = "Returned final response.",
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                }, cancellationToken);

                var usage = modelResult.Usage is null
                    ? null
                    : modelResult.Usage with { Duration = DateTimeOffset.UtcNow - startedAtUtc };

                await _runStore.CompleteRunAsync(
                    context.RunId,
                    AgentRunStatus.Completed,
                    DateTimeOffset.UtcNow,
                    stepSummaries.Count,
                    usage,
                    null,
                    null,
                    cancellationToken);

                return new AgentRunResult(
                    AgentRunStatus.Completed,
                    modelResult.FinalText,
                    context.ConversationId,
                    context.RunId,
                    citations.Values.ToList(),
                    null,
                    stepSummaries,
                    usage,
                    null);
            }

            var nextContinuationItems = new List<ModelConversationItem>();

            foreach (var toolCall in modelResult.ToolCalls)
            {
                var duplicateKey = $"{toolCall.ToolName}:{toolCall.ArgumentsJson}";
                if (!seenToolCalls.Add(duplicateKey))
                {
                    return await FailAsync(context, stepSummaries, $"Detected repeated tool call {toolCall.ToolName}.", "duplicate_tool_call", cancellationToken);
                }

                if (!_toolRegistry.TryGet(toolCall.ToolName, out var tool) || tool is null)
                {
                    return await FailAsync(context, stepSummaries, $"Unknown tool requested: {toolCall.ToolName}.", "unknown_tool", cancellationToken);
                }

                var validation = tool.Validate(toolCall.ArgumentsJson);
                if (!validation.IsValid || validation.NormalizedArgumentsJson is null)
                {
                    return await FailAsync(
                        context,
                        stepSummaries,
                        $"Tool arguments for {tool.Definition.Name} were invalid: {string.Join("; ", validation.Errors)}",
                        "tool_validation_failed",
                        cancellationToken);
                }

                if (!RoleHelpers.MeetsMinimumRole(context.User.Role, tool.Definition.MinimumRole))
                {
                    return await FailAsync(
                        context,
                        stepSummaries,
                        $"User role {context.User.Role} is not authorized to run {tool.Definition.Name}.",
                        "tool_unauthorized",
                        cancellationToken);
                }

                using var arguments = JsonDocument.Parse(validation.NormalizedArgumentsJson);

                if (tool.Definition.RequiresApproval)
                {
                    var approval = await _approvalService.CreateAsync(
                        context.ConversationId,
                        context.RunId,
                        tool.Definition.Name,
                        toolCall.CallId,
                        tool.BuildActionSummary(arguments),
                        validation.NormalizedArgumentsJson,
                        context.User.Email,
                        tool.Definition.MinimumRole,
                        modelResult.ResponseId,
                        cancellationToken);

                    var summary = new AgentStepSummary(stepNumber, "ApprovalRequested", $"Awaiting approval for {tool.Definition.Name}.", tool.Definition.Name, true, true);
                    stepSummaries.Add(summary);

                    var step = new AgentStep
                    {
                        AgentRunId = context.RunId,
                        StepNumber = stepNumber,
                        StepType = "ApprovalRequested",
                        Summary = $"Awaiting approval for {tool.Definition.Name}.",
                        StartedAtUtc = startedAtUtc,
                        CompletedAtUtc = DateTimeOffset.UtcNow
                    };

                    await _runStore.AddStepAsync(step, cancellationToken);
                    await _runStore.AddToolExecutionAsync(new ToolExecution
                    {
                        AgentRunId = context.RunId,
                        AgentStepId = step.Id,
                        ToolName = tool.Definition.Name,
                        ToolCallId = toolCall.CallId,
                        ValidatedArgumentsJson = validation.NormalizedArgumentsJson,
                        ResultJson = """{"pendingApproval":true}""",
                        Success = true,
                        RequiresApproval = true,
                        StartedAtUtc = startedAtUtc,
                        CompletedAtUtc = DateTimeOffset.UtcNow
                    }, cancellationToken);

                    var pendingApproval = await _approvalService.GetViewModelAsync(approval.Id, cancellationToken);
                    await _runStore.CompleteRunAsync(
                        context.RunId,
                        AgentRunStatus.AwaitingApproval,
                        DateTimeOffset.UtcNow,
                        stepSummaries.Count,
                        modelResult.Usage,
                        null,
                        approval.Id,
                        cancellationToken);

                    return new AgentRunResult(
                        AgentRunStatus.AwaitingApproval,
                        null,
                        context.ConversationId,
                        context.RunId,
                        citations.Values.ToList(),
                        pendingApproval,
                        stepSummaries,
                        modelResult.Usage,
                        null);
                }

                var execution = await ExecuteToolAsync(
                    context,
                    stepNumber,
                    tool,
                    toolCall.CallId,
                    arguments,
                    approvalGranted: false,
                    cancellationToken);

                stepSummaries.Add(new AgentStepSummary(
                    stepNumber,
                    "ToolCall",
                    execution.Summary,
                    tool.Definition.Name,
                    tool.Definition.RequiresApproval,
                    execution.Success));

                foreach (var citation in execution.Citations)
                {
                    citations[citation.CitationId] = citation;
                }

                await _conversationStore.AddMessageAsync(
                    context.ConversationId,
                    AgentMessageKind.Tool,
                    tool.Definition.Name,
                    execution.Summary,
                    tool.Definition.Name,
                    execution.StructuredDataJson,
                    cancellationToken);

                nextContinuationItems.Add(new ModelToolResultItem(toolCall.CallId, tool.Definition.Name, execution.OutputJson));
            }

            continuationItems = nextContinuationItems;
        }

        await _runStore.CompleteRunAsync(
            context.RunId,
            AgentRunStatus.MaximumStepsExceeded,
            DateTimeOffset.UtcNow,
            stepSummaries.Count,
            null,
            "maximum_steps_exceeded",
            null,
            cancellationToken);

        return new AgentRunResult(
            AgentRunStatus.MaximumStepsExceeded,
            "The agent stopped because it reached the configured maximum number of steps.",
            context.ConversationId,
            context.RunId,
            citations.Values.ToList(),
            null,
            stepSummaries,
            null,
            "maximum_steps_exceeded");
    }

    internal static IReadOnlyList<ModelConversationItem> BuildSafeTranscriptInputItems(IReadOnlyList<AgentMessage> messages)
    {
        var items = new List<ModelConversationItem>();
        var latestUserIndex = -1;

        for (var index = 0; index < messages.Count; index++)
        {
            if (messages[index].Kind == AgentMessageKind.User)
            {
                latestUserIndex = index;
            }
        }

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            switch (message.Kind)
            {
                case AgentMessageKind.User:
                    items.Add(new ModelMessageItem("user", message.Content));
                    break;
                case AgentMessageKind.Assistant:
                    items.Add(new ModelMessageItem("assistant", message.Content));
                    break;
                case AgentMessageKind.System:
                    break;
                case AgentMessageKind.Tool:
                    if (index > latestUserIndex && message.StructuredDataJson is not null)
                    {
                        using var document = JsonDocument.Parse(message.StructuredDataJson);
                        var root = document.RootElement;
                        var callId = root.GetProperty("callId").GetString() ?? string.Empty;
                        var toolName = root.GetProperty("toolName").GetString() ?? string.Empty;
                        var outputJson = root.GetProperty("outputJson").GetString() ?? "{}";
                        items.Add(new ModelToolResultItem(callId, toolName, outputJson));
                    }

                    break;
            }
        }

        return items;
    }

    private async Task<ToolExecutionOutcome> ExecuteToolAsync(
        AgentContext context,
        int stepNumber,
        ITool tool,
        string toolCallId,
        JsonDocument arguments,
        bool approvalGranted,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            var result = await tool.ExecuteAsync(
                new ToolExecutionContext(
                    context.ConversationId,
                    context.RunId,
                    context.CorrelationId,
                    context.User,
                    approvalGranted),
                arguments,
                cancellationToken);

            var step = new AgentStep
            {
                AgentRunId = context.RunId,
                StepNumber = stepNumber,
                StepType = "ToolCall",
                Summary = result.Summary,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ErrorCode = result.ErrorCode
            };

            await _runStore.AddStepAsync(step, cancellationToken);
            await _runStore.AddToolExecutionAsync(new ToolExecution
            {
                AgentRunId = context.RunId,
                AgentStepId = step.Id,
                ToolName = tool.Definition.Name,
                ToolCallId = toolCallId,
                ValidatedArgumentsJson = arguments.RootElement.GetRawText(),
                ResultJson = result.ResultJson,
                Success = result.Success,
                RequiresApproval = tool.Definition.RequiresApproval,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ErrorCode = result.ErrorCode
            }, cancellationToken);

            var structuredDataJson = ToolJsonEnvelope(toolCallId, tool.Definition.Name, result.ResultJson);
            return new ToolExecutionOutcome(
                result.Success,
                result.Summary,
                result.Citations,
                structuredDataJson,
                result.ResultJson,
                result.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} failed", tool.Definition.Name);

            await _runStore.AddStepAsync(new AgentStep
            {
                AgentRunId = context.RunId,
                StepNumber = stepNumber,
                StepType = "ToolCall",
                Summary = $"Tool {tool.Definition.Name} failed.",
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ErrorCode = "tool_exception"
            }, cancellationToken);

            throw;
        }
    }

    private async Task<AgentRunResult> FailAsync(
        AgentContext context,
        IReadOnlyList<AgentStepSummary> stepSummaries,
        string message,
        string errorCode,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("{ErrorCode}: {Message}", errorCode, message);

        await _conversationStore.AddMessageAsync(
            context.ConversationId,
            AgentMessageKind.Assistant,
            _agent.Definition.Name,
            message,
            null,
            null,
            cancellationToken);

        await _runStore.CompleteRunAsync(
            context.RunId,
            AgentRunStatus.Failed,
            DateTimeOffset.UtcNow,
            stepSummaries.Count,
            null,
            errorCode,
            null,
            cancellationToken);

        return new AgentRunResult(
            AgentRunStatus.Failed,
            message,
            context.ConversationId,
            context.RunId,
            [],
            null,
            stepSummaries,
            null,
            errorCode);
    }

    private static string ToolJsonEnvelope(string callId, string toolName, string outputJson)
    {
        return JsonSerializer.Serialize(new
        {
            callId,
            toolName,
            outputJson
        });
    }

    private static AuthenticatedUserContext CreateConversationOwnerContext(string ownerEmail, ClubRole role)
    {
        return new AuthenticatedUserContext(Guid.Empty, ownerEmail, ownerEmail, role);
    }

    private sealed record ToolExecutionOutcome(
        bool Success,
        string Summary,
        IReadOnlyList<AgentCitation> Citations,
        string StructuredDataJson,
        string OutputJson,
        string? ErrorCode);
}
