namespace RigMatch.Api.Models;

public sealed record CompanyProjectListItem(
    Guid Id,
    string Title,
    string PrimaryRole,
    string Location,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
