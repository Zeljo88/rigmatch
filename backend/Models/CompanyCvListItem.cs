namespace RigMatch.Api.Models;

public sealed record CompanyCvListItem(
    Guid Id,
    string Name,
    string LatestTitle,
    string? HighestEducation,
    int? ExperienceYears,
    DateTimeOffset CreatedAtUtc,
    bool IsFinalized,
    bool HasNeedsReview,
    bool IsMatchReady,
    string ReviewStatus);
