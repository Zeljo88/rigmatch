namespace RigMatch.Api.Models;

public sealed record CompanyProjectDetailResponse(
    Guid Id,
    string Title,
    string ClientName,
    string PrimaryRole,
    IReadOnlyList<string> AdditionalRoles,
    IReadOnlyList<string> RequiredSkills,
    IReadOnlyList<string> PreferredSkills,
    IReadOnlyList<string> RequiredCertifications,
    IReadOnlyList<string> PreferredCertifications,
    int? MinimumExperienceYears,
    string Location,
    string PreferredEducation,
    string Description,
    string Status,
    DateTimeOffset? StartDateUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<CompanyProjectCandidateMatch> CandidateMatches);
