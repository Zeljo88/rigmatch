namespace RigMatch.Api.Data.Entities;

public class CvRecord
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = default!;

    public string FileUrl { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;

    public string ParsedDraftJson { get; set; } = string.Empty;

    public string? FinalJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
