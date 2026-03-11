namespace RigMatch.Api.Data.Entities;

public class SuggestedRoleAlias
{
    public int Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = default!;

    public Guid? LastCvRecordId { get; set; }

    public CvRecord? LastCvRecord { get; set; }

    public int StandardRoleId { get; set; }

    public StandardRole StandardRole { get; set; } = default!;

    public string RawAlias { get; set; } = string.Empty;

    public string RawAliasNormalized { get; set; } = string.Empty;

    public int ConfirmationCount { get; set; } = 1;

    public DateTimeOffset FirstSuggestedAtUtc { get; set; }

    public DateTimeOffset LastSuggestedAtUtc { get; set; }
}
