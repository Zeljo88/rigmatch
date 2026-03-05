namespace RigMatch.Api.Models;

public sealed record CompanyCvUploadResponse(
    Guid Id,
    string FileUrl,
    ParsedCandidateProfile ParsedProfile,
    DateTimeOffset CreatedAtUtc);
