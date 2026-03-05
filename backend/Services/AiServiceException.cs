namespace RigMatch.Api.Services;

public sealed class AiServiceException : Exception
{
    public AiServiceException(string message, int statusCode, int? retryAfterSeconds = null)
        : base(message)
    {
        StatusCode = statusCode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int StatusCode { get; }

    public int? RetryAfterSeconds { get; }
}
