namespace RigMatch.Api.Models;

public sealed record CurrentEmployerResponse(
    Guid UserId,
    string FullName,
    string Email,
    Guid CompanyId,
    string CompanyName);
