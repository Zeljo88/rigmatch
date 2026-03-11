namespace RigMatch.Api.Services;

public interface ICvParsingGate
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
