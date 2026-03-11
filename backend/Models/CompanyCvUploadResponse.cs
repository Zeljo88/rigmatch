namespace RigMatch.Api.Models;

public sealed record CompanyCvUploadResponse(
    Guid Id,
    string FileUrl,
    ParsedCandidateProfile ParsedProfile,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<CompanyCvDuplicateWarning> DuplicateWarnings);

public sealed record CompanyCvDuplicateWarning(
    string Type,
    string Message,
    Guid ExistingCvId);
