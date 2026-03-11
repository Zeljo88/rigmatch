namespace RigMatch.Api.Services;

public sealed class CvParsingGate : ICvParsingGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _sync = new();
    private readonly ICvDiagnosticsLogger _diagnosticsLogger;
    private DateTimeOffset? _cooldownUntilUtc;

    public CvParsingGate(ICvDiagnosticsLogger diagnosticsLogger)
    {
        _diagnosticsLogger = diagnosticsLogger;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            ThrowIfCoolingDown();

            try
            {
                return await operation(cancellationToken);
            }
            catch (AiServiceException ex) when (ex.StatusCode == 429)
            {
                RegisterCooldown(ex.RetryAfterSeconds);
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ThrowIfCoolingDown()
    {
        DateTimeOffset? cooldownUntilUtc;
        lock (_sync)
        {
            cooldownUntilUtc = _cooldownUntilUtc;
            if (cooldownUntilUtc.HasValue && cooldownUntilUtc.Value <= DateTimeOffset.UtcNow)
            {
                _cooldownUntilUtc = null;
                cooldownUntilUtc = null;
            }
        }

        if (!cooldownUntilUtc.HasValue)
        {
            return;
        }

        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((cooldownUntilUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
        _ = _diagnosticsLogger.LogAsync(
            "gate.cooldown-active",
            $"retryAfterSeconds={retryAfterSeconds}",
            CancellationToken.None);
        throw new AiServiceException(
            $"Azure OpenAI cooldown is active. Please retry after {retryAfterSeconds} seconds.",
            429,
            retryAfterSeconds);
    }

    private void RegisterCooldown(int? retryAfterSeconds)
    {
        var waitSeconds = Math.Max(1, retryAfterSeconds ?? 30);
        var nextCooldown = DateTimeOffset.UtcNow.AddSeconds(waitSeconds);

        lock (_sync)
        {
            if (!_cooldownUntilUtc.HasValue || nextCooldown > _cooldownUntilUtc.Value)
            {
                _cooldownUntilUtc = nextCooldown;
            }
        }

        _ = _diagnosticsLogger.LogAsync(
            "gate.cooldown-registered",
            $"retryAfterSeconds={waitSeconds} cooldownUntilUtc={nextCooldown:O}",
            CancellationToken.None);
    }
}
