namespace RigMatch.Api.Services;

public interface ICvDiagnosticsLogger
{
    Task LogAsync(string eventName, string message, CancellationToken cancellationToken = default);
}
