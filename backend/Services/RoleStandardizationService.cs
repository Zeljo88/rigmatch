using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;

namespace RigMatch.Api.Services;

public sealed class RoleStandardizationService : IRoleStandardizationService
{
    private const double AutoAssignThreshold = 0.85d;
    private readonly RigMatchDbContext _dbContext;

    public RoleStandardizationService(RigMatchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoleMatchResult> MatchRoleAsync(string? rawRole, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            return new RoleMatchResult(string.Empty, null, string.Empty, 0d, true);
        }

        var trimmedRaw = rawRole.Trim();
        var normalized = Normalize(trimmedRaw);

        var standardRoles = await _dbContext.StandardRoles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => new StandardRoleLookup(r.Id, r.Name))
            .ToArrayAsync(cancellationToken);

        var aliasMatch = await _dbContext.RoleAliases
            .AsNoTracking()
            .Where(a => a.AliasNormalized == normalized && a.StandardRole.IsActive)
            .Select(a => new StandardRoleLookup(a.StandardRole.Id, a.StandardRole.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (aliasMatch is not null)
        {
            var confidence = 0.97d;
            return new RoleMatchResult(trimmedRaw, aliasMatch.Id, aliasMatch.Name, confidence, confidence < AutoAssignThreshold);
        }

        var directMatch = standardRoles
            .FirstOrDefault(role => Normalize(role.Name) == normalized);

        if (directMatch is not null)
        {
            var confidence = 0.94d;
            return new RoleMatchResult(trimmedRaw, directMatch.Id, directMatch.Name, confidence, confidence < AutoAssignThreshold);
        }

        var heuristic = HeuristicMatch(normalized, standardRoles);
        if (heuristic is not null)
        {
            var confidence = 0.78d;
            return new RoleMatchResult(trimmedRaw, heuristic.Id, heuristic.Name, confidence, confidence < AutoAssignThreshold);
        }

        var fallbackConfidence = 0.45d;
        return new RoleMatchResult(trimmedRaw, null, ToTitleCase(trimmedRaw), fallbackConfidence, fallbackConfidence < AutoAssignThreshold);
    }

    public async Task<string> StandardizeRoleAsync(string? rawRole, CancellationToken cancellationToken = default)
    {
        var match = await MatchRoleAsync(rawRole, cancellationToken);
        return match.StandardRoleName;
    }

    public async Task<IReadOnlyList<string>> StandardizeRoleListAsync(
        IEnumerable<string>? rawRoles,
        CancellationToken cancellationToken = default)
    {
        if (rawRoles is null)
        {
            return [];
        }

        var standardized = new List<string>();
        foreach (var role in rawRoles)
        {
            var normalizedRole = await StandardizeRoleAsync(role, cancellationToken);
            if (!string.IsNullOrWhiteSpace(normalizedRole))
            {
                standardized.Add(normalizedRole);
            }
        }

        return standardized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> GetStandardRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.StandardRoles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToArrayAsync(cancellationToken);
    }

    private static StandardRoleLookup? HeuristicMatch(string normalizedRole, IReadOnlyList<StandardRoleLookup> standardRoles)
    {
        bool Has(params string[] tokens) => tokens.All(t => normalizedRole.Contains(t, StringComparison.Ordinal));
        StandardRoleLookup? FindRole(string roleName) => standardRoles.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));

        if (Has("petroleum", "engineer", "intern"))
        {
            return FindRole("Petroleum Engineer Intern");
        }

        if (Has("petroleum", "engineer", "senior"))
        {
            return FindRole("Senior Petroleum Engineer");
        }

        if (Has("petroleum", "engineer", "junior"))
        {
            return FindRole("Junior Petroleum Engineer");
        }

        if (Has("petroleum", "engineer"))
        {
            return FindRole("Petroleum Engineer");
        }

        if (Has("petroleum", "operator", "senior"))
        {
            return FindRole("Senior Petroleum Operator");
        }

        if (Has("petroleum", "operator", "junior"))
        {
            return FindRole("Junior Petroleum Operator");
        }

        if (Has("petroleum", "operator"))
        {
            return FindRole("Petroleum Operator");
        }

        if (Has("project", "manager"))
        {
            return FindRole("Project Manager");
        }

        if (Has("interface", "manager"))
        {
            return FindRole("Interface Manager");
        }

        if (Has("drilling", "engineer"))
        {
            return FindRole("Drilling Engineer");
        }

        if (Has("wellsite", "supervisor"))
        {
            return FindRole("Wellsite Supervisor");
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return RoleCatalogSeeder.Normalize(value);
    }

    private static string ToTitleCase(string value)
    {
        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        var lower = normalized.ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
    }

    private sealed record StandardRoleLookup(int Id, string Name);
}
