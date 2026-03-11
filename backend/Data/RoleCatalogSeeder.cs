using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data.Entities;

namespace RigMatch.Api.Data;

public static class RoleCatalogSeeder
{
    private static readonly string[] StandardRoleNames =
    [
        "Petroleum Engineer",
        "Production Engineer",
        "Reservoir Engineer",
        "Subsurface Engineer",
        "Well Engineer",
        "Drilling Engineer",
        "Completion Engineer",
        "Workover Engineer",
        "Well Intervention Engineer",
        "Well Integrity Engineer",
        "Geologist",
        "Geophysicist",
        "Petrophysicist",
        "HSE Officer",
        "HSE Engineer",
        "Process Engineer",
        "Reliability Engineer",
        "Commissioning Engineer",
        "Facilities Engineer",
        "Mechanical Engineer",
        "Electrical Engineer",
        "Instrumentation & Control Engineer",
        "Pipeline Engineer",
        "Piping Engineer",
        "Construction Engineer",
        "Project Manager",
        "Project Engineer",
        "Interface Manager",
        "Planning Engineer",
        "Cost Engineer",
        "Contracts Engineer",
        "Procurement Engineer",
        "Operations Engineer",
        "Production Operator",
        "Senior Production Operator",
        "Petroleum Operator",
        "Well Site Supervisor",
        "Field Supervisor",
        "Drilling Supervisor",
        "Workover Supervisor",
        "Maintenance Engineer",
        "Offshore Installation Manager",
        "Drilling Superintendent",
        "Operations Manager"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> HardAliasesByRole = new Dictionary<string, string[]>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["Petroleum Engineer"] =
        [
            "Petroleum Eng",
            "Pet Eng",
            "Field Petroleum Engineer",
            "Petroleum Engineering Specialist",
            "Petroleum Engineering Advisor",
            "Petroleum Technical Engineer",
            "Upstream Petroleum Engineer",
            "Petroleum Development Engineer",
            "Asset Petroleum Engineer",
            "Petroleum Engineer I"
        ],
        ["Production Engineer"] =
        [
            "Production Eng",
            "Prod Eng",
            "Production Optimization Engineer",
            "Production Optimisation Engineer",
            "Well Performance Engineer",
            "Production Technologist",
            "Field Production Engineer",
            "Asset Production Engineer",
            "Production Support Engineer"
        ],
        ["Reservoir Engineer"] =
        [
            "Reservoir Eng",
            "Res Eng",
            "Reservoir Simulation Engineer",
            "Reservoir Management Engineer",
            "Reservoir Development Engineer",
            "Reservoir Surveillance Engineer",
            "Senior Reservoir Engineer",
            "Asset Reservoir Engineer",
            "Reservoir Studies Engineer",
            "Reservoir Performance Engineer"
        ],
        ["Subsurface Engineer"] =
        [
            "Subsurface Eng",
            "Sub Surface Engineer",
            "Sub-Surface Engineer",
            "Subsurface Specialist"
        ],
        ["Well Engineer"] =
        [
            "Well Eng",
            "Wells Engineer",
            "Wells Engineer",
            "Senior Well Engineer",
            "Well Engineering Specialist",
            "Well Delivery Engineer",
            "Well Design Engineer"
        ],
        ["Drilling Engineer"] =
        [
            "Drilling Eng",
            "Drill Engineer",
            "Well Drilling Engineer",
            "Senior Drilling Engineer",
            "Field Drilling Engineer",
            "Drilling Operations Engineer",
            "Drilling Performance Engineer",
            "Onshore Drilling Engineer",
            "Offshore Drilling Engineer",
            "Drilling & Well Planning Engineer",
            "Drilling and Wells Engineer"
        ],
        ["Completion Engineer"] =
        [
            "Completion Eng",
            "Completions Engineer",
            "Well Completion Engineer",
            "Senior Completion Engineer",
            "Completion Design Engineer",
            "Completion Operations Engineer",
            "Well Completion Specialist",
            "Field Completion Engineer",
            "Completion Technical Engineer"
        ],
        ["Workover Engineer"] =
        [
            "Workover Eng",
            "Well Workover Engineer",
            "Workover Specialist",
            "Workover Operations Engineer"
        ],
        ["Well Intervention Engineer"] =
        [
            "Well Intervention Eng",
            "Intervention Engineer",
            "Well Services Engineer",
            "Intervention Operations Engineer"
        ],
        ["Well Integrity Engineer"] =
        [
            "Well Integrity Eng",
            "Well Barrier Engineer",
            "Integrity Engineer",
            "Well Integrity Specialist"
        ],
        ["Geologist"] =
        [
            "Exploration Geologist",
            "Development Geologist",
            "Operations Geologist"
        ],
        ["Geophysicist"] =
        [
            "Exploration Geophysicist",
            "Development Geophysicist",
            "Geophysical Interpreter"
        ],
        ["Petrophysicist"] =
        [
            "Petrophysics Specialist",
            "Formation Evaluation Specialist",
            "Petrophysics Engineer"
        ],
        ["HSE Officer"] =
        [
            "Safety Officer",
            "HSSE Coordinator",
            "HSE Coordinator",
            "HSE Representative",
            "HSE Rep",
            "Site Safety Lead"
        ],
        ["HSE Engineer"] =
        [
            "HSE Eng",
            "HSSE Engineer",
            "EHS Engineer",
            "Safety Engineer",
            "Health Safety Environment Engineer",
            "Health, Safety and Environment Engineer",
            "SHE Engineer",
            "Project HSE Engineer",
            "Site HSE Engineer"
        ],
        ["Process Engineer"] =
        [
            "Process Eng",
            "Process Operations Engineer",
            "Process Design Engineer",
            "Senior Process Engineer",
            "Facilities Process Engineer",
            "Plant Process Engineer",
            "Operations Process Engineer",
            "Chemical Process Engineer"
        ],
        ["Reliability Engineer"] =
        [
            "Reliability Eng",
            "Asset Reliability Engineer",
            "Equipment Reliability Engineer"
        ],
        ["Commissioning Engineer"] =
        [
            "Commissioning Eng",
            "Start-up Engineer",
            "Startup Engineer",
            "Pre-Commissioning Engineer"
        ],
        ["Facilities Engineer"] =
        [
            "Facilities Eng",
            "Surface Facilities Engineer",
            "Oil & Gas Facilities Engineer",
            "Field Facilities Engineer",
            "Facilities Development Engineer",
            "Facilities Project Engineer",
            "Production Facilities Engineer",
            "Asset Facilities Engineer",
            "Facilities Operations Engineer",
            "Surface Engineer"
        ],
        ["Mechanical Engineer"] =
        [
            "Mechanical Eng",
            "Static Equipment Engineer",
            "Rotating Equipment Engineer"
        ],
        ["Electrical Engineer"] =
        [
            "Electrical Eng",
            "Power Systems Engineer"
        ],
        ["Instrumentation & Control Engineer"] =
        [
            "I&C Engineer",
            "Instrumentation Engineer",
            "Control Systems Engineer",
            "Instrument Engineer",
            "Controls Engineer",
            "Instrumentation and Control Engineer"
        ],
        ["Pipeline Engineer"] =
        [
            "Pipeline Eng",
            "Line Pipe Engineer",
            "Pipeline Integrity Engineer"
        ],
        ["Piping Engineer"] =
        [
            "Piping Eng",
            "Piping Stress Engineer"
        ],
        ["Construction Engineer"] =
        [
            "Construction Eng",
            "Site Construction Engineer"
        ],
        ["Project Manager"] =
        [
            "Project Mgr",
            "Project Lead"
        ],
        ["Project Engineer"] =
        [
            "Project Eng",
            "Package Engineer"
        ],
        ["Interface Manager"] =
        [
            "Interface Mgr",
            "External Interface Manager",
            "Internal Interface Manager"
        ],
        ["Planning Engineer"] =
        [
            "Planner Engineer",
            "Planning & Scheduling Engineer",
            "Project Planner"
        ],
        ["Cost Engineer"] =
        [
            "Cost Eng",
            "Project Cost Engineer",
            "Cost Control Engineer"
        ],
        ["Contracts Engineer"] =
        [
            "Contracts Eng",
            "Contract Engineer",
            "Commercial Contracts Engineer"
        ],
        ["Procurement Engineer"] =
        [
            "Procurement Eng",
            "Purchasing Engineer",
            "Strategic Sourcing Engineer"
        ],
        ["Operations Engineer"] =
        [
            "Ops Engineer",
            "Operations Support Engineer",
            "Field Operations Engineer",
            "Operations Eng",
            "Asset Operations Engineer",
            "Site Operations Engineer",
            "Operations Technical Engineer",
            "Operations Readiness Engineer"
        ],
        ["Production Operator"] =
        [
            "Process Operator",
            "Production Technician"
        ],
        ["Senior Production Operator"] =
        [
            "Lead Production Operator",
            "Senior Operator"
        ],
        ["Petroleum Operator"] =
        [
            "Wellhead Operator",
            "Field Operator"
        ],
        ["Well Site Supervisor"] =
        [
            "Wellsite Supervisor",
            "Company Man",
            "Rig Site Supervisor"
        ],
        ["Field Supervisor"] =
        [
            "Field Team Lead",
            "Site Field Supervisor",
            "Field Lead"
        ],
        ["Drilling Supervisor"] =
        [
            "Night Drilling Supervisor",
            "Senior Drilling Supervisor",
            "Drilling Superviser"
        ],
        ["Workover Supervisor"] =
        [
            "Well Intervention Supervisor",
            "Workover Superviser",
            "Workover Lead"
        ],
        ["Maintenance Engineer"] =
        [
            "Maintenance Eng",
            "Asset Maintenance Engineer"
        ],
        ["Offshore Installation Manager"] =
        [
            "OIM",
            "Installation Manager Offshore"
        ],
        ["Drilling Superintendent"] =
        [
            "Drilling Superintendant"
        ],
        ["Operations Manager"] =
        [
            "Ops Manager",
            "Field Operations Manager",
            "Production Operations Manager"
        ]
    };

    private static readonly IReadOnlyDictionary<string, string[]> SoftAliasesByRole = new Dictionary<string, string[]>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["Production Engineer"] =
        [
            "Production Operations Engineer"
        ],
        ["Completion Engineer"] =
        [
            "Completion and Workover Engineer"
        ],
        ["Well Intervention Engineer"] =
        [
            "Well Operations Engineer",
            "Wireline Intervention Engineer",
            "Coiled Tubing Engineer",
            "Well Stimulation Engineer"
        ],
        ["Process Engineer"] =
        [
            "Process Safety Engineer"
        ],
        ["HSE Engineer"] =
        [
            "QHSE Engineer"
        ],
        ["Operations Engineer"] =
        [
            "Operations Coordinator Engineer"
        ],
        ["Maintenance Engineer"] =
        [
            "Mechanical Maintenance Engineer",
            "Electrical Maintenance Engineer"
        ]
    };

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
                RequiresReview INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (StandardRoleId) REFERENCES StandardRoles(Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_RoleAliases_AliasNormalized ON RoleAliases(AliasNormalized);
            """, cancellationToken);

        await EnsureStandardRolesAsync(dbContext, cancellationToken);
        await EnsureRoleAliasesAsync(dbContext, cancellationToken);
    }

    public static string Normalize(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        lower = Regex.Replace(lower, @"[^\w\s&/+.-]", " ");
        return Regex.Replace(lower, @"\s+", " ").Trim();
    }

    private static async Task EnsureStandardRolesAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        var targetNames = new HashSet<string>(StandardRoleNames, StringComparer.OrdinalIgnoreCase);
        var existingRoles = await dbContext.StandardRoles.ToListAsync(cancellationToken);
        var existingMap = existingRoles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var role in existingRoles)
        {
            var shouldBeActive = targetNames.Contains(role.Name);
            if (role.IsActive != shouldBeActive)
            {
                role.IsActive = shouldBeActive;
                changed = true;
            }
        }

        foreach (var roleName in StandardRoleNames)
        {
            if (existingMap.ContainsKey(roleName))
            {
                continue;
            }

            dbContext.StandardRoles.Add(new StandardRole
            {
                Name = roleName,
                IsActive = true
            });
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureRoleAliasesAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        var roleMap = await dbContext.StandardRoles
            .Where(role => role.IsActive)
            .ToDictionaryAsync(role => role.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingAliases = await dbContext.RoleAliases.ToListAsync(cancellationToken);
        var existingByNormalized = existingAliases.ToDictionary(alias => alias.AliasNormalized, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        changed |= AddAliases(dbContext, roleMap, existingByNormalized, HardAliasesByRole, requiresReview: false);
        changed |= AddAliases(dbContext, roleMap, existingByNormalized, SoftAliasesByRole, requiresReview: true);

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool AddAliases(
        RigMatchDbContext dbContext,
        IReadOnlyDictionary<string, StandardRole> roleMap,
        IDictionary<string, RoleAlias> existingByNormalized,
        IReadOnlyDictionary<string, string[]> aliasesByRole,
        bool requiresReview)
    {
        var changed = false;

        foreach (var (roleName, aliases) in aliasesByRole)
        {
            if (!roleMap.TryGetValue(roleName, out var role))
            {
                continue;
            }

            foreach (var alias in aliases)
            {
                var normalizedAlias = Normalize(alias);
                if (normalizedAlias.Length == 0)
                {
                    continue;
                }

                if (existingByNormalized.TryGetValue(normalizedAlias, out var existingAlias))
                {
                    if (existingAlias.StandardRoleId != role.Id ||
                        existingAlias.RequiresReview != requiresReview ||
                        !string.Equals(existingAlias.Alias, alias, StringComparison.Ordinal))
                    {
                        existingAlias.StandardRoleId = role.Id;
                        existingAlias.Alias = alias;
                        existingAlias.AliasNormalized = normalizedAlias;
                        existingAlias.RequiresReview = requiresReview;
                        changed = true;
                    }

                    continue;
                }

                var created = new RoleAlias
                {
                    StandardRoleId = role.Id,
                    Alias = alias,
                    AliasNormalized = normalizedAlias,
                    RequiresReview = requiresReview
                };

                dbContext.RoleAliases.Add(created);
                existingByNormalized[normalizedAlias] = created;
                changed = true;
            }
        }

        return changed;
    }
}
