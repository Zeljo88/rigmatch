namespace RigMatch.Api.Data.Entities;

public class EmployerUser
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = default!;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string EmailNormalized { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
