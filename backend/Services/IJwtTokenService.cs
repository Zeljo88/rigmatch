using RigMatch.Api.Data.Entities;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public interface IJwtTokenService
{
    AuthResponse CreateToken(EmployerUser user, Company company);
}
