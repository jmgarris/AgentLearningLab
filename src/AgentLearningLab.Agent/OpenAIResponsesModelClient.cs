#pragma warning disable OPENAI001

using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using System.ClientModel;
using System.Text.Json;

namespace AgentLearningLab.Agent;

public sealed class OpenAIResponsesModelClient : IModelClient
{
    private readonly ILogger<OpenAIResponsesModelClient> _logger;
    private readonly OpenAIOptions _options;
    private readonly ResponsesClient _responsesClient;

    public OpenAIResponsesModelClient(
        string apiKey,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIResponsesModelClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _responsesClient = new ResponsesClient(new ApiKeyCredential(apiKey));
    }

    public async Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var options = new CreateResponseOptions
                {
                    Model = request.Model,
                    Instructions = request.Instructions,
                    PreviousResponseId = request.PreviousResponseId,
                    StoredOutputEnabled = false,
                    MaxToolCallCount = 8
                };

                foreach (var item in request.InputItems.Select(MapInputItem))
                {
                    options.InputItems.Add(item);
                }

                foreach (var tool in request.Tools.Select(MapTool))
                {
                    options.Tools.Add(tool);
                }

                var response = await _responsesClient.CreateResponseAsync(options, cancellationToken);
                return MapResponse(response);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                _logger.LogWarning(ex, "Transient OpenAI Responses error on attempt {Attempt}", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("OpenAI Responses request failed after bounded retries.");
    }

    private static bool IsTransient(Exception exception)
    {
        var name = exception.GetType().Name;
        var message = exception.Message;

        return name.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || name.Contains("RateLimit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("429", StringComparison.OrdinalIgnoreCase)
            || message.Contains("500", StringComparison.OrdinalIgnoreCase)
            || message.Contains("502", StringComparison.OrdinalIgnoreCase)
            || message.Contains("503", StringComparison.OrdinalIgnoreCase)
            || message.Contains("504", StringComparison.OrdinalIgnoreCase);
    }

    private static ResponseItem MapInputItem(ModelConversationItem item)
        => item switch
        {
            ModelMessageItem message when string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
                => ResponseItem.CreateUserMessageItem(message.Content),
            ModelMessageItem message when string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                => ResponseItem.CreateAssistantMessageItem(message.Content, []),
            ModelMessageItem message when string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase)
                => ResponseItem.CreateSystemMessageItem(message.Content),
            ModelMessageItem message when string.Equals(message.Role, "developer", StringComparison.OrdinalIgnoreCase)
                => ResponseItem.CreateDeveloperMessageItem(message.Content),
            ModelToolResultItem tool => ResponseItem.CreateFunctionCallOutputItem(tool.CallId, tool.OutputJson),
            _ => throw new InvalidOperationException("Unsupported model input item.")
        };

    private static ResponseTool MapTool(ModelToolDefinition tool)
        => ResponseTool.CreateFunctionTool(
            functionName: tool.Name,
            functionParameters: BinaryData.FromString(tool.JsonSchema),
            strictModeEnabled: true,
            functionDescription: tool.Description);

    private static ModelTurnResult MapResponse(object response)
    {
        var toolCalls = new List<ModelToolCall>();
        string? finalText = null;

        var responseType = response.GetType();
        var outputItems = responseType.GetProperty("OutputItems")?.GetValue(response) as System.Collections.IEnumerable;

        if (outputItems is not null)
        {
            foreach (var item in outputItems)
            {
                if (item is null)
                {
                    continue;
                }

                var itemType = item.GetType();
                if (string.Equals(itemType.Name, "FunctionCallResponseItem", StringComparison.Ordinal))
                {
                    toolCalls.Add(new ModelToolCall(
                        itemType.GetProperty("CallId")?.GetValue(item)?.ToString() ?? string.Empty,
                        itemType.GetProperty("FunctionName")?.GetValue(item)?.ToString() ?? string.Empty,
                        itemType.GetProperty("FunctionArguments")?.GetValue(item)?.ToString() ?? "{}"));
                }
                else if (string.Equals(itemType.Name, "MessageResponseItem", StringComparison.Ordinal))
                {
                    var role = itemType.GetProperty("Role")?.GetValue(item)?.ToString();
                    if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        finalText ??= itemType.GetProperty("ContentText")?.GetValue(item)?.ToString();
                    }
                }
            }
        }

        var usageObject = responseType.GetProperty("Usage")?.GetValue(response);
        var usage = usageObject is null
            ? null
            : new AgentUsage(
                usageObject.GetType().GetProperty("InputTokenCount")?.GetValue(usageObject) as int?,
                usageObject.GetType().GetProperty("OutputTokenCount")?.GetValue(usageObject) as int?,
                usageObject.GetType().GetProperty("TotalTokenCount")?.GetValue(usageObject) as int?,
                TimeSpan.Zero);

        var responseId = responseType.GetProperty("Id")?.GetValue(response)?.ToString();
        return new ModelTurnResult(responseId, finalText, toolCalls, usage);
    }
}
