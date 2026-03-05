using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public interface ICvParsingService
{
    Task<ParsedCandidateProfile> ParseCvTextAsync(string cvText, CancellationToken cancellationToken = default);
}
