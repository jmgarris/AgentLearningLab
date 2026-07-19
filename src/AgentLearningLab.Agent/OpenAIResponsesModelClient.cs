#pragma warning disable OPENAI001

using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using System.ClientModel;
using Microsoft.Extensions.Hosting;

namespace AgentLearningLab.Agent;

public sealed class OpenAIResponsesModelClient : IApiModelClient
{
    private readonly string _apiKey;
    private readonly IHostEnvironment? _hostEnvironment;
    private readonly ILogger<OpenAIResponsesModelClient> _logger;
    private readonly OpenAIOptions _options;
    private readonly IResponseResultMapper _responseResultMapper;
    private readonly ResponsesClient? _responsesClient;

    internal OpenAIResponsesModelClient(
        string apiKey,
        IOptions<OpenAIOptions> options,
        IResponseResultMapper responseResultMapper,
        ILogger<OpenAIResponsesModelClient> logger,
        IHostEnvironment? hostEnvironment = null)
    {
        _apiKey = apiKey;
        _options = options.Value;
        _responseResultMapper = responseResultMapper;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _responsesClient = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : new ResponsesClient(new ApiKeyCredential(apiKey));
    }

    public async Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not set. Switch back to Offline mode or configure an API key.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException("OpenAI model is not configured. Set OpenAI:Model or OpenAI__Model before using API key mode.");
        }

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
                    StoredOutputEnabled = true,
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

                ClientResult<ResponseResult> clientResult =
                    await _responsesClient!.CreateResponseAsync(options, cancellationToken);

                var response = clientResult.Value;

                try
                {
                    return _responseResultMapper.Map(response);
                }
                catch (OpenAIResponseException ex)
                {
                    LogDevelopmentDiagnostics(clientResult, response, ex);
                    throw;
                }
            }
            catch (OpenAIResponseException)
            {
                throw;
            }
            catch (ClientResultException ex) when (IsModelAccessError(ex, request.Model))
            {
                throw new InvalidOperationException(
                    $"The configured OpenAI project does not have access to model '{request.Model}'. Choose a different model or switch to Offline mode.",
                    ex);
            }
            catch (ClientResultException ex) when (IsAuthenticationError(ex))
            {
                throw new InvalidOperationException(
                    "The OpenAI request was not authenticated. Verify OPENAI_API_KEY for this PowerShell session or switch back to Offline mode.",
                    ex);
            }
            catch (ClientResultException ex) when (IsQuotaError(ex))
            {
                throw new InvalidOperationException(
                    "The OpenAI project has exhausted quota or credits for this request.",
                    ex);
            }
            catch (ClientResultException ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                _logger.LogWarning(ex, "Transient OpenAI Responses error on attempt {Attempt}", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (ClientResultException ex) when (IsRateLimit(ex))
            {
                throw new InvalidOperationException(
                    "The OpenAI request hit a rate limit. Wait briefly and try again.",
                    ex);
            }
            catch (ClientResultException ex) when (IsPreviousResponseInvalid(ex, request))
            {
                LogRequestFailure(ex, request, "openai_previous_response_invalid");
                throw new OpenAIRequestException(
                    "openai_previous_response_invalid",
                    "The saved OpenAI conversation state could not be continued. The application reset the live state; try the message again.",
                    ex.Status,
                    TryGetOpenAiErrorCode(ex),
                    TryGetParameterName(ex),
                    previousResponseIdSupplied: !string.IsNullOrWhiteSpace(request.PreviousResponseId),
                    ex);
            }
            catch (ClientResultException ex) when (IsOrphanedToolOutput(ex, request))
            {
                LogRequestFailure(ex, request, "openai_orphaned_tool_output");
                throw new OpenAIRequestException(
                    "openai_orphaned_tool_output",
                    "The conversation contained a tool output without its matching function call. Start a new conversation or reset its live API state.",
                    ex.Status,
                    TryGetOpenAiErrorCode(ex),
                    TryGetParameterName(ex),
                    previousResponseIdSupplied: !string.IsNullOrWhiteSpace(request.PreviousResponseId),
                    ex);
            }
            catch (ClientResultException ex) when (IsToolSchemaInvalid(ex))
            {
                LogRequestFailure(ex, request, "openai_tool_schema_invalid");
                throw new OpenAIRequestException(
                    "openai_tool_schema_invalid",
                    "OpenAI rejected the tool schema for this request.",
                    ex.Status,
                    TryGetOpenAiErrorCode(ex),
                    TryGetParameterName(ex),
                    previousResponseIdSupplied: !string.IsNullOrWhiteSpace(request.PreviousResponseId),
                    ex);
            }
            catch (ClientResultException ex) when (IsMalformedRequest(ex))
            {
                LogRequestFailure(ex, request, "openai_request_malformed");
                throw new OpenAIRequestException(
                    "openai_request_malformed",
                    "OpenAI rejected the request as malformed. Check the configured model and tool schema.",
                    ex.Status,
                    TryGetOpenAiErrorCode(ex),
                    TryGetParameterName(ex),
                    previousResponseIdSupplied: !string.IsNullOrWhiteSpace(request.PreviousResponseId),
                    ex);
            }
            catch (ClientResultException ex)
            {
                throw new InvalidOperationException(
                    "OpenAI Responses failed before a usable result was produced.",
                    ex);
            }
        }

        throw new InvalidOperationException("OpenAI Responses request failed after bounded retries.");
    }

    private static bool IsTransient(ClientResultException exception)
    {
        var message = exception.Message;

        return exception.Status is 408 or 500 or 502 or 503 or 504
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsModelAccessError(ClientResultException exception, string modelName)
    {
        var message = exception.Message;
        return exception.Status is 403 or 404
            && (message.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not have access to model", StringComparison.OrdinalIgnoreCase)
            || (message.Contains(modelName, StringComparison.OrdinalIgnoreCase)
                && message.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsAuthenticationError(ClientResultException exception)
    {
        var message = exception.Message;
        return exception.Status == 401
            || message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("incorrect api key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuotaError(ClientResultException exception)
    {
        var message = exception.Message;
        return exception.Status == 429
            && (message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase)
                || message.Contains("billing quota", StringComparison.OrdinalIgnoreCase)
                || message.Contains("run out of credits", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no balance", StringComparison.OrdinalIgnoreCase)
                || message.Contains("current quota", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRateLimit(ClientResultException exception)
    {
        var message = exception.Message;
        return exception.Status == 429
            && !IsQuotaError(exception)
            && (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || message.Contains("retry", StringComparison.OrdinalIgnoreCase)
                || message.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
                || message.Contains("tokens per minute", StringComparison.OrdinalIgnoreCase)
                || message.Contains("requests per minute", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMalformedRequest(ClientResultException exception)
    {
        var message = exception.Message;
        return exception.Status == 400
            || (exception.Status == 403
                && !IsModelAccessError(exception, string.Empty)
                && message.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase))
            || message.Contains("tool schema", StringComparison.OrdinalIgnoreCase)
            || message.Contains("json schema", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsToolSchemaInvalid(ClientResultException exception)
    {
        var message = exception.Message;
        return message.Contains("tool schema", StringComparison.OrdinalIgnoreCase)
            || message.Contains("json schema", StringComparison.OrdinalIgnoreCase)
            || message.Contains("function parameters", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreviousResponseInvalid(ClientResultException exception, ModelTurnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PreviousResponseId))
        {
            return false;
        }

        var message = exception.Message;
        return message.Contains("previous_response_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("previous_response_not_found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("response not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("stored response", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot be resolved", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOrphanedToolOutput(ClientResultException exception, ModelTurnRequest request)
    {
        if (!request.InputItems.OfType<ModelToolResultItem>().Any())
        {
            return false;
        }

        var message = exception.Message;
        return string.IsNullOrWhiteSpace(request.PreviousResponseId)
            && (message.Contains("function_call_output", StringComparison.OrdinalIgnoreCase)
                || message.Contains("tool output", StringComparison.OrdinalIgnoreCase)
                || message.Contains("call_id", StringComparison.OrdinalIgnoreCase)
                || message.Contains("function call", StringComparison.OrdinalIgnoreCase));
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

    private void LogDevelopmentDiagnostics(
        ClientResult<ResponseResult> clientResult,
        ResponseResult response,
        OpenAIResponseException exception)
    {
        _logger.LogWarning(
            exception,
            "OpenAI response mapping failed with {ErrorCode} for response {ResponseId} status {Status}",
            exception.ErrorCode,
            response.Id,
            response.Status?.ToString() ?? "Unknown");

        if (!_options.EnableDevelopmentResponseDiagnostics || !string.Equals(_hostEnvironment?.EnvironmentName, Environments.Development, StringComparison.Ordinal))
        {
            return;
        }

        var rawResponse = clientResult.GetRawResponse().ToString();
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return;
        }

        const int maxLength = 4000;
        var truncated = rawResponse.Length <= maxLength
            ? rawResponse
            : $"{rawResponse[..maxLength]}...[truncated]";

        _logger.LogDebug(
            "Development-only OpenAI raw response snapshot for {ResponseId}: {RawResponse}",
            response.Id,
            truncated);
    }

    private void LogRequestFailure(
        ClientResultException exception,
        ModelTurnRequest request,
        string errorCode)
    {
        _logger.LogWarning(
            exception,
            "OpenAI request failure {ErrorCode} status {Status} model {Model} previousResponseIdSupplied {PreviousResponseIdSupplied} inputItemTypes {@InputItemTypes} toolNames {@ToolNames}",
            errorCode,
            exception.Status,
            request.Model,
            !string.IsNullOrWhiteSpace(request.PreviousResponseId),
            request.InputItems.Select(static item => item.GetType().Name).ToArray(),
            request.Tools.Select(static tool => tool.Name).ToArray());
    }

    private static string? TryGetOpenAiErrorCode(ClientResultException exception)
    {
        var message = exception.Message;
        if (message.Contains("previous_response_not_found", StringComparison.OrdinalIgnoreCase))
        {
            return "previous_response_not_found";
        }

        if (message.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase))
        {
            return "invalid_request_error";
        }

        return null;
    }

    private static string? TryGetParameterName(ClientResultException exception)
    {
        var message = exception.Message;
        if (message.Contains("previous_response_id", StringComparison.OrdinalIgnoreCase))
        {
            return "previous_response_id";
        }

        return null;
    }
}
