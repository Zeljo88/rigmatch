using System.Text;

namespace RigMatch.Api.Services;

public sealed class FileCvDiagnosticsLogger : ICvDiagnosticsLogger
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _logPath;

    public FileCvDiagnosticsLogger()
    {
        var appServiceHome = Environment.GetEnvironmentVariable("HOME");
        var directory = !string.IsNullOrWhiteSpace(appServiceHome)
            ? Path.Combine(appServiceHome, "LogFiles", "RigMatch")
            : Path.Combine(Path.GetTempPath(), "RigMatch", "logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "cv-diagnostics.log");
    }

    public async Task LogAsync(string eventName, string message, CancellationToken cancellationToken = default)
    {
        var line = $"{DateTimeOffset.UtcNow:O} [{eventName}] {message}{Environment.NewLine}";

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logPath, line, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
