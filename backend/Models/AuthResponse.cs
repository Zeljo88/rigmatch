namespace RigMatch.Api.Models;

public sealed record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string Email,
    Guid CompanyId,
    string CompanyName);
