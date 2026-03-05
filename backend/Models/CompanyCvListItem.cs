namespace RigMatch.Api.Models;

public sealed record CompanyCvListItem(
    Guid Id,
    string Name,
    string LatestTitle,
    string? Location,
    int? ExperienceYears,
    DateTimeOffset CreatedAtUtc,
    bool IsFinalized);
