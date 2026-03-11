namespace RigMatch.Api.Models;

public sealed class SaveCompanyProjectRequest
{
    public string? Title { get; set; }

    public string? ClientName { get; set; }

    public string? PrimaryRole { get; set; }

    public IReadOnlyList<string>? AdditionalRoles { get; set; }

    public IReadOnlyList<string>? RequiredSkills { get; set; }

    public IReadOnlyList<string>? PreferredSkills { get; set; }

    public IReadOnlyList<string>? RequiredCertifications { get; set; }

    public IReadOnlyList<string>? PreferredCertifications { get; set; }

    public int? MinimumExperienceYears { get; set; }

    public string? Location { get; set; }

    public string? PreferredEducation { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset? StartDateUtc { get; set; }
}
