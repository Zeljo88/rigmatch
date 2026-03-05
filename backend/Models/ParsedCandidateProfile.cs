namespace RigMatch.Api.Models;

public sealed record ParsedCandidateProfile(
    string Name,
    string Email,
    IReadOnlyList<string> JobTitles,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Certifications,
    int ExperienceYears);
