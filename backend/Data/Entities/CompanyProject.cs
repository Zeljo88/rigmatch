namespace RigMatch.Api.Data.Entities;

public class CompanyProject
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = default!;

    public string Title { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string PrimaryRole { get; set; } = string.Empty;

    public string AdditionalRolesJson { get; set; } = "[]";

    public string RequiredSkillsJson { get; set; } = "[]";

    public string PreferredSkillsJson { get; set; } = "[]";

    public string RequiredCertificationsJson { get; set; } = "[]";

    public string PreferredCertificationsJson { get; set; } = "[]";

    public int? MinimumExperienceYears { get; set; }

    public string Location { get; set; } = string.Empty;

    public string PreferredEducation { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public DateTimeOffset? StartDateUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
