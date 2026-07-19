#pragma warning disable OPENAI001

using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using System.Text;
using System.Text.Json;

namespace AgentLearningLab.Agent;

internal interface IResponseResultMapper
{
    ModelTurnResult Map(ResponseResult response);
}

internal sealed class ResponseResultMapper(ILogger<ResponseResultMapper> logger) : IResponseResultMapper
{
    public ModelTurnResult Map(ResponseResult response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var responseId = response.Id;
        var responseStatus = response.Status?.ToString();
        var outputItems = response.OutputItems ?? [];
        var outputItemTypes = outputItems.Select(static item => item.GetType().Name).ToArray();

        if (IsStatus(response.Status, ResponseStatus.Incomplete))
        {
            throw CreateIncompleteException(response);
        }

        if (IsStatus(response.Status, ResponseStatus.Failed) || IsStatus(response.Status, ResponseStatus.Cancelled))
        {
            throw CreateFailedException(response);
        }

        var toolCalls = new List<ModelToolCall>();
        var textBuilder = new StringBuilder();
        var refusalBuilder = new StringBuilder();
        var textPartCount = 0;
        var refusalPartCount = 0;
        var reasoningItemCount = 0;

        foreach (var outputItem in outputItems)
        {
            switch (outputItem)
            {
                case ReasoningResponseItem:
                    reasoningItemCount++;
                    break;
                case MessageResponseItem message:
                    if (IsAssistantMessage(message))
                    {
                        AppendMessageContent(message, textBuilder, refusalBuilder, ref textPartCount, ref refusalPartCount);
                    }
                    break;
                case FunctionCallResponseItem functionCall:
                    toolCalls.Add(MapFunctionCall(functionCall, responseId, responseStatus));
                    break;
            }
        }

        var finalText = textBuilder.Length > 0
            ? textBuilder.ToString()
            : refusalBuilder.Length > 0
                ? refusalBuilder.ToString()
                : null;

        var usage = response.Usage is null
            ? null
            : new AgentUsage(
                response.Usage.InputTokenCount,
                response.Usage.OutputTokenCount,
                response.Usage.TotalTokenCount,
                TimeSpan.Zero);

        logger.LogDebug(
            "OpenAI response {ResponseId} status {Status} model {Model} outputCount {OutputCount} outputItemTypes {@OutputItemTypes} textParts {TextPartCount} refusals {RefusalPartCount} functionCalls {FunctionCallCount} reasoningItems {ReasoningItemCount} promptTokens {PromptTokens} completionTokens {CompletionTokens} totalTokens {TotalTokens}",
            responseId,
            responseStatus ?? "Unknown",
            response.Model,
            outputItems.Count,
            outputItemTypes,
            textPartCount,
            refusalPartCount,
            toolCalls.Count,
            reasoningItemCount,
            usage?.PromptTokens,
            usage?.CompletionTokens,
            usage?.TotalTokens);

        if (toolCalls.Count == 0 && string.IsNullOrWhiteSpace(finalText))
        {
            throw new OpenAIResponseMappingException(
                "openai_response_output_unrecognized",
                "OpenAI returned a completed response, but this version of the application could not recognize its output items.",
                responseId,
                responseStatus);
        }

        return new ModelTurnResult(responseId, finalText, toolCalls, usage);
    }

    private static void AppendMessageContent(
        MessageResponseItem message,
        StringBuilder textBuilder,
        StringBuilder refusalBuilder,
        ref int textPartCount,
        ref int refusalPartCount)
    {
        foreach (var contentPart in message.Content)
        {
            if (!string.IsNullOrWhiteSpace(contentPart.Text))
            {
                textBuilder.Append(contentPart.Text);
                textPartCount++;
            }

            if (!string.IsNullOrWhiteSpace(contentPart.Refusal))
            {
                refusalBuilder.Append(contentPart.Refusal);
                refusalPartCount++;
            }
        }
    }

    private static ModelToolCall MapFunctionCall(
        FunctionCallResponseItem functionCall,
        string? responseId,
        string? responseStatus)
    {
        if (string.IsNullOrWhiteSpace(functionCall.CallId))
        {
            throw new OpenAIResponseMappingException(
                "openai_function_call_missing_call_id",
                "OpenAI returned a function call without a call ID.",
                responseId,
                responseStatus);
        }

        if (string.IsNullOrWhiteSpace(functionCall.FunctionName))
        {
            throw new OpenAIResponseMappingException(
                "openai_function_call_missing_name",
                "OpenAI returned a function call without a function name.",
                responseId,
                responseStatus);
        }

        var argumentsJson = functionCall.FunctionArguments.ToString();
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            argumentsJson = "{}";
        }

        try
        {
            using var _ = JsonDocument.Parse(argumentsJson);
        }
        catch (JsonException)
        {
            throw new OpenAIResponseMappingException(
                "openai_function_call_invalid_arguments",
                "OpenAI returned function-call arguments that were not valid JSON.",
                responseId,
                responseStatus);
        }

        return new ModelToolCall(functionCall.CallId, functionCall.FunctionName, argumentsJson);
    }

    private static OpenAIResponseIncompleteException CreateIncompleteException(ResponseResult response)
    {
        var reason = response.IncompleteStatusDetails?.Reason?.ToString();
        var message = string.IsNullOrWhiteSpace(reason)
            ? "The OpenAI response was incomplete before assistant text or a tool call was produced."
            : $"The OpenAI response was incomplete before assistant text or a tool call was produced. Reason: {reason}.";

        return new OpenAIResponseIncompleteException(
            "openai_response_incomplete",
            message,
            response.Id,
            response.Status?.ToString());
    }

    private static OpenAIResponseFailedException CreateFailedException(ResponseResult response)
    {
        var errorCode = response.Error is null ? null : response.Error.Code.ToString();
        var errorMessage = response.Error?.Message;
        var detail = string.IsNullOrWhiteSpace(errorCode)
            ? errorMessage
            : $"{errorCode}: {errorMessage}";
        var message = string.IsNullOrWhiteSpace(detail)
            ? "The OpenAI response failed before a usable result was produced."
            : $"The OpenAI response failed before a usable result was produced. {detail}";

        return new OpenAIResponseFailedException(
            "openai_response_failed",
            message,
            response.Id,
            response.Status?.ToString());
    }

    private static bool IsStatus(ResponseStatus? actual, ResponseStatus expected)
        => string.Equals(actual?.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsAssistantMessage(MessageResponseItem message)
        => string.Equals(message.Role.ToString(), MessageRole.Assistant.ToString(), StringComparison.OrdinalIgnoreCase);
}
