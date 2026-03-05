namespace RigMatch.Api.Models;

public sealed record CompanyCvDetailResponse(
    Guid Id,
    string FileUrl,
    string StructuredProfileJson,
    bool IsFinalized,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string DownloadUrl);
