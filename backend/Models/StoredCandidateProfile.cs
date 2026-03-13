namespace RigMatch.Api.Models;

public sealed class StoredCandidateProfile
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string HighestEducation { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public IReadOnlyList<string> JobTitles { get; set; } = [];

    public IReadOnlyList<string> Companies { get; set; } = [];

    public IReadOnlyList<string> Skills { get; set; } = [];

    public IReadOnlyList<string> Certifications { get; set; } = [];

    public int ExperienceYears { get; set; }

    public IReadOnlyList<ParsedExperienceEntry> Experiences { get; set; } = [];

    public IReadOnlyList<RoleExperienceBreakdownItem> RoleExperience { get; set; } = [];
}
