using System.Globalization;
using System.Text.RegularExpressions;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public static class RoleExperienceCalculator
{
    public static IReadOnlyList<RoleExperienceBreakdownItem> Calculate(IEnumerable<ParsedExperienceEntry>? experiences)
    {
        if (experiences is null)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var buckets = new Dictionary<string, (string JobTitle, double Years)>(StringComparer.OrdinalIgnoreCase);

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

            if (!TryParseDate(experience.EndDate, now, out var end))
            {
                end = now;
            }

            if (end < start)
            {
                continue;
            }

            var years = (end - start).TotalDays / 365.25d;
            if (years <= 0.01d)
            {
                continue;
            }

            var key = NormalizeRoleKey(role);
            if (buckets.TryGetValue(key, out var current))
            {
                buckets[key] = (current.JobTitle, current.Years + years);
            }
            else
            {
                buckets[key] = (role, years);
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

    private static string NormalizeRoleKey(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        return Regex.Replace(trimmed, @"\s+", " ");
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
}
