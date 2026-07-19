namespace AgentLearningLab.Agent;

public abstract class OpenAIResponseException : InvalidOperationException
{
    protected OpenAIResponseException(
        string errorCode,
        string message,
        string? responseId,
        string? responseStatus,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ResponseId = responseId;
        ResponseStatus = responseStatus;
    }

    public string ErrorCode { get; }

    public string? ResponseId { get; }

    public string? ResponseStatus { get; }
}

public sealed class OpenAIResponseIncompleteException(
    string errorCode,
    string message,
    string? responseId,
    string? responseStatus)
    : OpenAIResponseException(errorCode, message, responseId, responseStatus);

public sealed class OpenAIResponseFailedException(
    string errorCode,
    string message,
    string? responseId,
    string? responseStatus,
    Exception? innerException = null)
    : OpenAIResponseException(errorCode, message, responseId, responseStatus, innerException);

public sealed class OpenAIResponseMappingException(
    string errorCode,
    string message,
    string? responseId,
    string? responseStatus)
    : OpenAIResponseException(errorCode, message, responseId, responseStatus);
