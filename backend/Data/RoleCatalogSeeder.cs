using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data.Entities;

namespace RigMatch.Api.Data;

public static class RoleCatalogSeeder
{
    public static async Task SeedAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS StandardRoles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_StandardRoles_Name ON StandardRoles(Name);
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS RoleAliases (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StandardRoleId INTEGER NOT NULL,
                Alias TEXT NOT NULL,
                AliasNormalized TEXT NOT NULL,
                FOREIGN KEY (StandardRoleId) REFERENCES StandardRoles(Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_RoleAliases_AliasNormalized ON RoleAliases(AliasNormalized);
            """, cancellationToken);

        var standardRoleNames = new[]
        {
            "Petroleum Engineer",
            "Senior Petroleum Engineer",
            "Junior Petroleum Engineer",
            "Petroleum Engineer Intern",
            "Reservoir Engineer",
            "Production Engineer",
            "Drilling Engineer",
            "Completion Engineer",
            "Well Intervention Engineer",
            "Well Integrity Engineer",
            "Subsurface Engineer",
            "Geologist",
            "Geophysicist",
            "Petrophysicist",
            "HSE Engineer",
            "HSE Officer",
            "Process Engineer",
            "Facilities Engineer",
            "Mechanical Engineer",
            "Electrical Engineer",
            "Instrumentation & Control Engineer",
            "Pipeline Engineer",
            "Piping Engineer",
            "Construction Engineer",
            "QA/QC Engineer",
            "Project Engineer",
            "Project Manager",
            "Interface Manager",
            "Planning Engineer",
            "Cost Engineer",
            "Contracts Engineer",
            "Procurement Engineer",
            "Operations Engineer",
            "Production Operator",
            "Senior Production Operator",
            "Petroleum Operator",
            "Senior Petroleum Operator",
            "Junior Petroleum Operator",
            "Wellsite Supervisor",
            "Field Supervisor",
            "Maintenance Engineer",
            "Reliability Engineer",
            "Commissioning Engineer",
            "Offshore Installation Manager",
            "Superintendent"
        };

        if (!await dbContext.StandardRoles.AnyAsync(cancellationToken))
        {
            var roles = standardRoleNames
                .Select(name => new StandardRole
                {
                    Name = name,
                    IsActive = true
                })
                .ToArray();

            dbContext.StandardRoles.AddRange(roles);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.RoleAliases.AnyAsync(cancellationToken))
        {
            var roleMap = await dbContext.StandardRoles
                .ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var seedAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sr pet eng"] = "Senior Petroleum Engineer",
                ["sr petroleum engineer"] = "Senior Petroleum Engineer",
                ["senior pet eng"] = "Senior Petroleum Engineer",
                ["jr pet eng"] = "Junior Petroleum Engineer",
                ["jr petroleum engineer"] = "Junior Petroleum Engineer",
                ["pet eng intern"] = "Petroleum Engineer Intern",
                ["petroleum engineer trainee"] = "Petroleum Engineer Intern",
                ["petroleum operator"] = "Petroleum Operator",
                ["sr petroleum operator"] = "Senior Petroleum Operator",
                ["jr petroleum operator"] = "Junior Petroleum Operator",
                ["project mgr"] = "Project Manager",
                ["interface mgr"] = "Interface Manager",
                ["company man"] = "Wellsite Supervisor",
                ["well site supervisor"] = "Wellsite Supervisor"
            };

            foreach (var (alias, roleName) in seedAliases)
            {
                if (!roleMap.TryGetValue(roleName, out var role))
                {
                    continue;
                }

                dbContext.RoleAliases.Add(new RoleAlias
                {
                    StandardRoleId = role.Id,
                    Alias = alias,
                    AliasNormalized = Normalize(alias)
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static string Normalize(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        lower = Regex.Replace(lower, @"[^\w\s&/+.-]", " ");
        return Regex.Replace(lower, @"\s+", " ").Trim();
    }
}
