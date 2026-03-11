using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;

namespace RigMatch.Api.Services;

public sealed class ParsingReferenceService : IParsingReferenceService
{
    private const int MaxReferenceRoles = 12;
    private const int MaxAliasesPerRole = 3;
    private readonly RigMatchDbContext _dbContext;

    public ParsingReferenceService(RigMatchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> BuildPromptReferenceBlockAsync(string cvText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cvText))
        {
            return string.Empty;
        }

        var normalizedCvText = Normalize(cvText);
        var roles = await _dbContext.StandardRoles
            .AsNoTracking()
            .Where(role => role.IsActive)
            .Select(role => new
            {
                role.Name,
                Aliases = role.Aliases
                    .Select(alias => new AliasLookup(alias.Alias, alias.AliasNormalized))
                    .ToArray()
            })
            .ToArrayAsync(cancellationToken);

        var relevant = roles
            .Select(role =>
            {
                var matchedAliases = role.Aliases
                    .Where(alias => ContainsWholePhrase(normalizedCvText, alias.AliasNormalized))
                    .Select(alias => alias.Alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxAliasesPerRole)
                    .ToArray();

                var roleNormalized = Normalize(role.Name);
                var roleMatched = ContainsWholePhrase(normalizedCvText, roleNormalized);
                var tokenScore = ScoreTokenOverlap(normalizedCvText, roleNormalized);

                return new
                {
                    role.Name,
                    roleMatched,
                    matchedAliases,
                    tokenScore
                };
            })
            .Where(item => item.roleMatched || item.matchedAliases.Length > 0 || item.tokenScore >= 2)
            .OrderByDescending(item => item.matchedAliases.Length)
            .ThenByDescending(item => item.roleMatched)
            .ThenByDescending(item => item.tokenScore)
            .ThenBy(item => item.Name)
            .Take(MaxReferenceRoles)
            .ToArray();

        if (relevant.Length == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>
        {
            "Relevant standard roles and aliases for this CV:"
        };

        foreach (var item in relevant)
        {
            if (item.matchedAliases.Length > 0)
            {
                lines.Add($"- {item.Name} <- {string.Join("; ", item.matchedAliases)}");
                continue;
            }

            lines.Add($"- {item.Name}");
        }

        return string.Join('\n', lines);
    }

    private static int ScoreTokenOverlap(string normalizedCvText, string normalizedRole)
    {
        var tokens = normalizedRole
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return tokens.Count(token => normalizedCvText.Contains(token, StringComparison.Ordinal));
    }

    private static bool ContainsWholePhrase(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(needle))
        {
            return false;
        }

        return haystack.Contains(needle, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return RoleCatalogSeeder.Normalize(value);
    }

    private sealed record AliasLookup(string Alias, string AliasNormalized);
}
