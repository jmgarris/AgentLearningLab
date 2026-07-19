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
            catch (ClientResultException ex) when (IsMalformedRequest(ex))
            {
                throw new InvalidOperationException(
                    "OpenAI rejected the request as malformed. Check the configured model and tool schema.",
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
}
