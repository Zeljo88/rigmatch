using System.Globalization;
using System.Text.RegularExpressions;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public static class RoleExperienceCalculator
{
    public static IReadOnlyList<RoleExperienceBreakdownItem> Calculate(IEnumerable<ParsedExperienceEntry>? experiences)
    {
        var resolvedExperiences = ResolveExperienceRanges(experiences);
        var buckets = new Dictionary<string, (string JobTitle, double Years)>(StringComparer.OrdinalIgnoreCase);

        foreach (var experience in resolvedExperiences)
        {
            var years = (experience.End - experience.Start).TotalDays / 365.25d;
            if (years <= 0.01d)
            {
                continue;
            }

            var key = NormalizeRoleKey(experience.Role);
            if (buckets.TryGetValue(key, out var current))
            {
                buckets[key] = (current.JobTitle, current.Years + years);
            }
            else
            {
                buckets[key] = (experience.Role, years);
            }
        }

        return buckets.Values
            .Select(entry => new RoleExperienceBreakdownItem(
                entry.JobTitle,
                Math.Round(entry.Years, 1, MidpointRounding.AwayFromZero)))
            .OrderByDescending(item => item.Years)
            .ThenBy(item => item.JobTitle, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static int CalculateTotalYears(IEnumerable<ParsedExperienceEntry>? experiences)
    {
        var resolvedExperiences = ResolveExperienceRanges(experiences)
            .OrderBy(item => item.Start)
            .ToArray();

        if (resolvedExperiences.Length == 0)
        {
            return 0;
        }

        var totalDays = 0d;
        var currentStart = resolvedExperiences[0].Start;
        var currentEnd = resolvedExperiences[0].End;

        foreach (var experience in resolvedExperiences.Skip(1))
        {
            if (experience.Start <= currentEnd)
            {
                if (experience.End > currentEnd)
                {
                    currentEnd = experience.End;
                }

                continue;
            }

            totalDays += (currentEnd - currentStart).TotalDays;
            currentStart = experience.Start;
            currentEnd = experience.End;
        }

        totalDays += (currentEnd - currentStart).TotalDays;
        var totalYears = totalDays / 365.25d;
        return (int)Math.Round(totalYears, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeRoleKey(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        return Regex.Replace(trimmed, @"\s+", " ");
    }

    private static IReadOnlyList<ResolvedExperienceRange> ResolveExperienceRanges(IEnumerable<ParsedExperienceEntry>? experiences)
    {
        if (experiences is null)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var draftRanges = new List<DraftExperienceRange>();

        foreach (var experience in experiences)
        {
            var role = (experience.StandardRoleName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(role))
            {
                role = (experience.RawRoleTitle ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            if (!TryParseDate(experience.StartDate, now, out var start))
            {
                continue;
            }

            var hasExplicitEnd = TryParseDate(experience.EndDate, now, out var end);
            var isCurrent = IsCurrentMarker(experience.EndDate);

            draftRanges.Add(new DraftExperienceRange(role, start, hasExplicitEnd ? end : null, isCurrent));
        }

        if (draftRanges.Count == 0)
        {
            return [];
        }

        var ordered = draftRanges
            .OrderBy(range => range.Start)
            .ToArray();

        var resolved = new List<ResolvedExperienceRange>(ordered.Length);
        for (var i = 0; i < ordered.Length; i++)
        {
            var current = ordered[i];
            var end = current.End;

            if (!end.HasValue)
            {
                if (current.IsCurrent || i == ordered.Length - 1)
                {
                    end = now;
                }
                else
                {
                    var nextStart = ordered[i + 1].Start;
                    end = nextStart > current.Start ? nextStart : current.Start;
                }
            }

            if (end.Value < current.Start)
            {
                continue;
            }

            resolved.Add(new ResolvedExperienceRange(current.Role, current.Start, end.Value));
        }

        return resolved;
    }

    private static bool IsCurrentMarker(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var input = raw.Trim();
        return string.Equals(input, "present", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(input, "current", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(input, "now", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDate(string? raw, DateTime now, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var input = raw.Trim();

        if (string.Equals(input, "present", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "current", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "now", StringComparison.OrdinalIgnoreCase))
        {
            value = now;
            return true;
        }

        if (DateTime.TryParseExact(
                input,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var yearMonth))
        {
            value = new DateTime(yearMonth.Year, yearMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        if (DateTime.TryParseExact(
                input,
                "yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var year))
        {
            value = new DateTime(year.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        return DateTime.TryParse(
            input,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }

    private sealed record DraftExperienceRange(
        string Role,
        DateTime Start,
        DateTime? End,
        bool IsCurrent);

    private sealed record ResolvedExperienceRange(
        string Role,
        DateTime Start,
        DateTime End);
}
