namespace RigMatch.Api.Models;

public sealed record CompanyProjectCandidateMatch(
    Guid CvId,
    string CandidateName,
    string CurrentRole,
    int ExperienceYears,
    int MatchScore,
    string RoleMatchType,
    IReadOnlyList<string> MatchedRequiredCertifications,
    IReadOnlyList<string> MissingRequiredCertifications,
    IReadOnlyList<string> MatchedPreferredCertifications,
    IReadOnlyList<string> MatchedRequiredSkills,
    IReadOnlyList<string> MissingRequiredSkills,
    IReadOnlyList<string> MatchedPreferredSkills,
    bool MeetsMinimumExperience,
    bool LocationMatched,
    bool EducationMatched,
    IReadOnlyList<string> SummaryPoints);
