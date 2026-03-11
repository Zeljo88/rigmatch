namespace RigMatch.Api.Services;

public interface IParsingReferenceService
{
    Task<string> BuildPromptReferenceBlockAsync(string cvText, CancellationToken cancellationToken = default);
}
