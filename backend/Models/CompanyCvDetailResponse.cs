namespace RigMatch.Api.Models;

public sealed record CompanyCvDetailResponse(
    Guid Id,
    string FileUrl,
    string StructuredProfileJson,
    bool IsFinalized,
    bool HasNeedsReview,
    bool IsMatchReady,
    string ReviewStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string DownloadUrl);
