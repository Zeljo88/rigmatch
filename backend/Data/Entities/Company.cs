namespace RigMatch.Api.Data.Entities;

public class Company
{
    public Guid Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<CvRecord> CvRecords { get; set; } = new List<CvRecord>();
}
