using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;
using RigMatch.Api.Data.Entities;
using RigMatch.Api.Models;
using RigMatch.Api.Services;

namespace RigMatch.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly RigMatchDbContext _dbContext;
    private readonly IPasswordHasher<EmployerUser> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        RigMatchDbContext dbContext,
        IPasswordHasher<EmployerUser> passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterEmployerRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Registration payload is required." });
        }

        if (string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Company name, full name, email, and password are required." });
        }

        if (request.Password.Trim().Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters long." });
        }

        var email = request.Email.Trim();
        var normalizedEmail = NormalizeEmail(email);

        var existingUser = await _dbContext.EmployerUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.EmailNormalized == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            return Conflict(new { message = "An employer account with this email already exists." });
        }

        var company = new Company
        {
            Id = Guid.NewGuid(),
            ExternalId = $"company-{Guid.NewGuid():N}",
            Name = request.CompanyName.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var user = new EmployerUser
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Company = company,
            FullName = request.FullName.Trim(),
            Email = email,
            EmailNormalized = normalizedEmail,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastLoginAtUtc = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password.Trim());

        _dbContext.Companies.Add(company);
        _dbContext.EmployerUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(_jwtTokenService.CreateToken(user, company));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginEmployerRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.EmployerUsers
            .Include(item => item.Company)
            .FirstOrDefaultAsync(item => item.EmailNormalized == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password.Trim());
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(_jwtTokenService.CreateToken(user, user.Company));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentEmployerResponse>> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var user = await _dbContext.EmployerUsers
            .Include(item => item.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        return Ok(new CurrentEmployerResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.CompanyId,
            user.Company.Name));
    }

    private static string NormalizeEmail(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
