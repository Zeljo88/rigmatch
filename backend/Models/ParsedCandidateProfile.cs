namespace RigMatch.Api.Models;

public sealed record ParsedCandidateProfile(
    string Name,
    string Email,
    string PhoneNumber,
    string HighestEducation,
    IReadOnlyList<string> JobTitles,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Certifications,
    int ExperienceYears,
    IReadOnlyList<ParsedExperienceEntry> Experiences,
    IReadOnlyList<RoleExperienceBreakdownItem> RoleExperience);

public sealed record ParsedExperienceEntry(
    string CompanyName,
    string RawRoleTitle,
    int? StandardRoleId,
    string StandardRoleName,
    double? MatchConfidence,
    bool NeedsReview,
    bool ReviewedByUser,
    string StartDate,
    string EndDate,
    string Description);

public sealed record RoleExperienceBreakdownItem(
    string JobTitle,
    double Years);
