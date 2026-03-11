using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;

namespace RigMatch.Api.Services;

public sealed class RoleStandardizationService : IRoleStandardizationService
{
    private const double AutoAssignThreshold = 0.85d;

    private static readonly IReadOnlyDictionary<string, RoleProfile> RoleProfiles =
        new Dictionary<string, RoleProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Petroleum Engineer"] = new(
                TitleSignals:
                [
                    "petroleum engineer", "petroleum eng", "pet eng", "upstream petroleum", "petroleum technical"
                ],
                PositiveDescriptionSignals:
                [
                    "field development", "asset development", "production sharing", "upstream", "well delivery", "development planning"
                ],
                NegativeDescriptionSignals:
                [
                    "reservoir simulation", "history matching", "drilling", "wireline", "hazop", "topsides"
                ]),
            ["Reservoir Engineer"] = new(
                TitleSignals:
                [
                    "reservoir engineer", "reservoir eng", "res eng", "reservoir simulation", "reservoir management"
                ],
                PositiveDescriptionSignals:
                [
                    "reservoir", "history matching", "material balance", "eclipse", "petrel", "reserves", "static model", "dynamic model", "forecasting"
                ],
                NegativeDescriptionSignals:
                [
                    "drilling", "completion", "wireline", "hazop", "operator rounds"
                ]),
            ["Production Engineer"] = new(
                TitleSignals:
                [
                    "production engineer", "production eng", "prod eng", "well performance", "production optimization", "production optimisation"
                ],
                PositiveDescriptionSignals:
                [
                    "artificial lift", "nodal", "well performance", "flow assurance", "production surveillance", "production optimization", "debottleneck", "production forecasting"
                ],
                NegativeDescriptionSignals:
                [
                    "reservoir simulation", "well control", "hazop", "ptw", "wireline"
                ]),
            ["Drilling Engineer"] = new(
                TitleSignals:
                [
                    "drilling engineer", "drilling eng", "drill engineer", "well drilling", "drilling performance"
                ],
                PositiveDescriptionSignals:
                [
                    "drilling", "well planning", "well control", "mud", "bop", "casing", "directional", "rig", "drill string"
                ],
                NegativeDescriptionSignals:
                [
                    "history matching", "artificial lift", "wireline", "hazop", "topsides"
                ]),
            ["Completion Engineer"] = new(
                TitleSignals:
                [
                    "completion engineer", "completion eng", "completions engineer", "well completion", "completion design"
                ],
                PositiveDescriptionSignals:
                [
                    "completion", "packer", "sand control", "perforation", "frac", "fracturing", "well completion", "lower completion", "upper completion"
                ],
                NegativeDescriptionSignals:
                [
                    "wireline intervention", "coiled tubing", "history matching", "hazop"
                ]),
            ["Well Intervention Engineer"] = new(
                TitleSignals:
                [
                    "well intervention", "intervention engineer", "workover engineer", "well services", "workover intervention"
                ],
                PositiveDescriptionSignals:
                [
                    "workover", "intervention", "wireline", "slickline", "coiled tubing", "stimulation", "well services", "acidizing", "fishing"
                ],
                NegativeDescriptionSignals:
                [
                    "history matching", "hazop", "topsides", "p&id"
                ]),
            ["Process Engineer"] = new(
                TitleSignals:
                [
                    "process engineer", "process eng", "process design", "chemical process", "process operations"
                ],
                PositiveDescriptionSignals:
                [
                    "process", "p&id", "pfd", "heat and mass balance", "chemical", "plant", "simulation", "hazop", "relief valve"
                ],
                NegativeDescriptionSignals:
                [
                    "wireline", "reservoir", "well control", "operator rounds"
                ]),
            ["Facilities Engineer"] = new(
                TitleSignals:
                [
                    "facilities engineer", "facilities eng", "surface facilities", "facilities development", "surface engineer"
                ],
                PositiveDescriptionSignals:
                [
                    "facilities", "surface", "utilities", "topsides", "brownfield", "tie-in", "gathering system", "pipeline network", "plant modification"
                ],
                NegativeDescriptionSignals:
                [
                    "history matching", "wireline", "artificial lift", "ptw"
                ]),
            ["HSE Engineer"] = new(
                TitleSignals:
                [
                    "hse engineer", "hse eng", "hsse engineer", "ehs engineer", "safety engineer", "qhse engineer"
                ],
                PositiveDescriptionSignals:
                [
                    "hse", "hsse", "ehs", "safety", "hazop", "hazid", "risk assessment", "permit to work", "incident investigation", "toolbox talk", "ptw"
                ],
                NegativeDescriptionSignals:
                [
                    "history matching", "artificial lift", "wireline", "petrel"
                ]),
            ["Operations Engineer"] = new(
                TitleSignals:
                [
                    "operations engineer", "ops engineer", "operations support", "operations eng", "operations readiness"
                ],
                PositiveDescriptionSignals:
                [
                    "operations support", "startup", "shutdown", "operability", "troubleshooting", "debottleneck", "operations readiness", "handover", "commissioning support"
                ],
                NegativeDescriptionSignals:
                [
                    "history matching", "wireline", "well control", "hazop", "material balance"
                ])
        };

    private readonly RigMatchDbContext _dbContext;

    public RoleStandardizationService(RigMatchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoleMatchResult> MatchRoleAsync(
        string? rawRole,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            return new RoleMatchResult(string.Empty, null, string.Empty, 0d, true, "empty", "rawRole empty");
        }

        var trimmedRaw = rawRole.Trim();
        var normalizedRole = Normalize(trimmedRaw);
        var normalizedDescription = Normalize(description ?? string.Empty);

        var standardRoles = await _dbContext.StandardRoles
            .AsNoTracking()
            .Where(role => role.IsActive)
            .Select(role => new StandardRoleLookup(role.Id, role.Name))
            .ToArrayAsync(cancellationToken);

        var aliasMatch = await _dbContext.RoleAliases
            .AsNoTracking()
            .Where(alias => alias.AliasNormalized == normalizedRole && alias.StandardRole.IsActive)
            .Select(alias => new AliasMatchLookup(alias.StandardRole.Id, alias.StandardRole.Name, alias.RequiresReview, alias.Alias))
            .FirstOrDefaultAsync(cancellationToken);

        if (aliasMatch is not null)
        {
            var confidence = aliasMatch.RequiresReview ? 0.78d : 0.95d;
            return new RoleMatchResult(
                trimmedRaw,
                aliasMatch.Id,
                aliasMatch.Name,
                confidence,
                aliasMatch.RequiresReview,
                aliasMatch.RequiresReview ? "soft-alias" : "alias",
                $"alias={aliasMatch.Alias}");
        }

        var directMatch = standardRoles.FirstOrDefault(role => Normalize(role.Name) == normalizedRole);
        if (directMatch is not null)
        {
            return new RoleMatchResult(trimmedRaw, directMatch.Id, directMatch.Name, 0.94d, false, "direct", "matched standard role name");
        }

        var heuristicMatch = HeuristicMatch(normalizedRole, standardRoles);
        if (heuristicMatch is not null)
        {
            return new RoleMatchResult(
                trimmedRaw,
                heuristicMatch.Id,
                heuristicMatch.Name,
                0.78d,
                true,
                "heuristic",
                $"normalizedRole={normalizedRole}");
        }

        var contextualMatch = ScoreRoleFromContext(normalizedRole, normalizedDescription, standardRoles);
        if (contextualMatch is not null)
        {
            return new RoleMatchResult(
                trimmedRaw,
                contextualMatch.Id,
                contextualMatch.Name,
                contextualMatch.Confidence,
                true,
                "context",
                contextualMatch.Details);
        }

        return new RoleMatchResult(trimmedRaw, null, string.Empty, 0.45d, true, "unmatched", "no alias/heuristic/context winner");
    }

    public async Task<string> StandardizeRoleAsync(
        string? rawRole,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var match = await MatchRoleAsync(rawRole, description, cancellationToken);
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
            var normalizedRole = await StandardizeRoleAsync(role, null, cancellationToken);
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
            .Where(role => role.IsActive)
            .OrderBy(role => role.Name)
            .Select(role => role.Name)
            .ToArrayAsync(cancellationToken);
    }

    private static StandardRoleLookup? HeuristicMatch(string normalizedRole, IReadOnlyList<StandardRoleLookup> standardRoles)
    {
        bool Has(params string[] tokens) => tokens.All(token => normalizedRole.Contains(token, StringComparison.Ordinal));
        bool Any(params string[] tokens) => tokens.Any(token => normalizedRole.Contains(token, StringComparison.Ordinal));
        StandardRoleLookup? FindRole(string roleName) =>
            standardRoles.FirstOrDefault(role => string.Equals(role.Name, roleName, StringComparison.OrdinalIgnoreCase));

        if (Has("petroleum", "engineer") || Has("pet", "eng"))
        {
            return FindRole("Petroleum Engineer");
        }

        if (Has("reservoir", "engineer") || Has("res", "eng"))
        {
            return FindRole("Reservoir Engineer");
        }

        if ((Has("production", "engineer") || Has("prod", "eng")) && !Any("operations"))
        {
            return FindRole("Production Engineer");
        }

        if (Has("drilling", "engineer") || Has("drill", "engineer"))
        {
            return FindRole("Drilling Engineer");
        }

        if (Has("completion", "engineer") || Has("completions", "engineer"))
        {
            return FindRole("Completion Engineer");
        }

        if (Has("intervention", "engineer") || Has("workover", "engineer"))
        {
            return FindRole("Well Intervention Engineer");
        }

        if (Has("process", "engineer"))
        {
            return FindRole("Process Engineer");
        }

        if (Has("facilities", "engineer"))
        {
            return FindRole("Facilities Engineer");
        }

        if (Any("hse", "hsse", "ehs", "safety") && Any("engineer", "eng"))
        {
            return FindRole("HSE Engineer");
        }

        if ((Has("operations", "engineer") || Has("ops", "engineer")) && !Has("production", "operations"))
        {
            return FindRole("Operations Engineer");
        }

        return null;
    }

    private static ContextualRoleMatch? ScoreRoleFromContext(
        string normalizedRole,
        string normalizedDescription,
        IReadOnlyList<StandardRoleLookup> standardRoles)
    {
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return null;
        }

        var roleScores = new List<RoleScore>();

        foreach (var role in standardRoles)
        {
            if (!RoleProfiles.TryGetValue(role.Name, out var profile))
            {
                continue;
            }

            var titleScore = CountMatches(normalizedRole, profile.TitleSignals);
            var positiveScore = CountMatches(normalizedDescription, profile.PositiveDescriptionSignals);
            var negativeScore = CountMatches(normalizedDescription, profile.NegativeDescriptionSignals);

            var totalScore = (titleScore * 3) + (positiveScore * 2) - (negativeScore * 2);
            roleScores.Add(new RoleScore(role, titleScore, positiveScore, negativeScore, totalScore));
        }

        var ordered = roleScores
            .Where(score => score.PositiveScore > 0 || score.TitleScore > 0)
            .OrderByDescending(score => score.TotalScore)
            .ThenByDescending(score => score.PositiveScore)
            .ThenByDescending(score => score.TitleScore)
            .ToArray();

        if (ordered.Length == 0)
        {
            return null;
        }

        var best = ordered[0];
        if (best.TotalScore < 4)
        {
            return null;
        }

        var second = ordered.Skip(1).FirstOrDefault();
        if (second is not null && best.TotalScore - second.TotalScore <= 1)
        {
            return null;
        }

        var confidence = CalculateConfidence(best);
        var detail = $"titleScore={best.TitleScore};positive={best.PositiveScore};negative={best.NegativeScore};total={best.TotalScore}";
        return new ContextualRoleMatch(best.Role.Id, best.Role.Name, confidence, detail);
    }

    private static int CountMatches(string text, IEnumerable<string> signals)
    {
        var count = 0;
        foreach (var signal in signals)
        {
            if (text.Contains(signal, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static double CalculateConfidence(RoleScore score)
    {
        var confidence = 0.58d;
        confidence += Math.Min(0.12d, score.TitleScore * 0.04d);
        confidence += Math.Min(0.18d, score.PositiveScore * 0.04d);
        confidence -= Math.Min(0.10d, score.NegativeScore * 0.04d);
        confidence += score.TotalScore >= 8 ? 0.04d : 0d;
        return Math.Clamp(confidence, 0.55d, 0.84d);
    }

    private static string Normalize(string value)
    {
        return RoleCatalogSeeder.Normalize(value);
    }

    private sealed record StandardRoleLookup(int Id, string Name);
    private sealed record AliasMatchLookup(int Id, string Name, bool RequiresReview, string Alias);
    private sealed record ContextualRoleMatch(int Id, string Name, double Confidence, string Details);
    private sealed record RoleProfile(
        IReadOnlyList<string> TitleSignals,
        IReadOnlyList<string> PositiveDescriptionSignals,
        IReadOnlyList<string> NegativeDescriptionSignals);
    private sealed record RoleScore(
        StandardRoleLookup Role,
        int TitleScore,
        int PositiveScore,
        int NegativeScore,
        int TotalScore);
}
