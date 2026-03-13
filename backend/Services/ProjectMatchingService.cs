using System.Text.Json;
using RigMatch.Api.Data;
using RigMatch.Api.Data.Entities;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public class ProjectMatchingService : IProjectMatchingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<CompanyProjectCandidateMatch> MatchCandidates(
        CompanyProject project,
        IReadOnlyList<CvRecord> finalizedCandidates)
    {
        var primaryRole = Normalize(project.PrimaryRole);
        var additionalRoles = DeserializeList(project.AdditionalRolesJson);
        var requiredSkills = DeserializeList(project.RequiredSkillsJson);
        var preferredSkills = DeserializeList(project.PreferredSkillsJson);
        var requiredCertifications = DeserializeList(project.RequiredCertificationsJson);
        var preferredCertifications = DeserializeList(project.PreferredCertificationsJson);

        var results = new List<CompanyProjectCandidateMatch>();
        foreach (var record in finalizedCandidates)
        {
            if (string.IsNullOrWhiteSpace(record.FinalJson))
            {
                continue;
            }

            StoredCandidateProfile? profile;
            try
            {
                profile = JsonSerializer.Deserialize<StoredCandidateProfile>(record.FinalJson, JsonOptions);
            }
            catch
            {
                continue;
            }

            if (profile is null)
            {
                continue;
            }

            var candidateRoles = CollectCandidateRoles(profile);
            var roleMatchType = "none";
            var score = 0;
            var summary = new List<string>();

            if (primaryRole.Length > 0 && candidateRoles.Contains(primaryRole))
            {
                score += 35;
                roleMatchType = "primary";
                summary.Add($"Primary role matched: {project.PrimaryRole}");
            }
            else
            {
                var matchedAdditionalRoles = additionalRoles
                    .Where(role => candidateRoles.Contains(Normalize(role)))
                    .ToArray();

                if (matchedAdditionalRoles.Length > 0)
                {
                    score += 20;
                    roleMatchType = "secondary";
                    summary.Add($"Acceptable role matched: {matchedAdditionalRoles[0]}");
                }
            }

            var requiresRoleMatch = primaryRole.Length > 0 || additionalRoles.Count > 0;
            if (requiresRoleMatch && roleMatchType == "none")
            {
                continue;
            }

            var matchedRequiredCertifications = MatchTerms(requiredCertifications, profile.Certifications);
            var missingRequiredCertifications = ExceptTerms(requiredCertifications, matchedRequiredCertifications);
            if (requiredCertifications.Count > 0)
            {
                score += (int)Math.Round(20d * matchedRequiredCertifications.Count / requiredCertifications.Count, MidpointRounding.AwayFromZero);
            }

            if (matchedRequiredCertifications.Count > 0)
            {
                summary.Add($"Required certifications matched: {string.Join(", ", matchedRequiredCertifications)}");
            }

            if (missingRequiredCertifications.Count > 0)
            {
                summary.Add($"Missing certifications: {string.Join(", ", missingRequiredCertifications)}");
            }

            var matchedPreferredCertifications = MatchTerms(preferredCertifications, profile.Certifications);
            if (preferredCertifications.Count > 0)
            {
                score += (int)Math.Round(8d * matchedPreferredCertifications.Count / preferredCertifications.Count, MidpointRounding.AwayFromZero);
            }

            if (matchedPreferredCertifications.Count > 0)
            {
                summary.Add($"Preferred certifications matched: {string.Join(", ", matchedPreferredCertifications)}");
            }

            var matchedRequiredSkills = MatchTerms(requiredSkills, profile.Skills);
            var missingRequiredSkills = ExceptTerms(requiredSkills, matchedRequiredSkills);
            if (requiredSkills.Count > 0)
            {
                score += (int)Math.Round(10d * matchedRequiredSkills.Count / requiredSkills.Count, MidpointRounding.AwayFromZero);
            }

            if (matchedRequiredSkills.Count > 0)
            {
                summary.Add($"Required skills matched: {string.Join(", ", matchedRequiredSkills)}");
            }

            if (missingRequiredSkills.Count > 0)
            {
                summary.Add($"Missing skills: {string.Join(", ", missingRequiredSkills)}");
            }

            var matchedPreferredSkills = MatchTerms(preferredSkills, profile.Skills);
            if (preferredSkills.Count > 0)
            {
                score += (int)Math.Round(5d * matchedPreferredSkills.Count / preferredSkills.Count, MidpointRounding.AwayFromZero);
            }

            if (matchedPreferredSkills.Count > 0)
            {
                summary.Add($"Preferred skills matched: {string.Join(", ", matchedPreferredSkills)}");
            }

            var meetsMinimumExperience = !project.MinimumExperienceYears.HasValue ||
                                         profile.ExperienceYears >= project.MinimumExperienceYears.Value;
            if (project.MinimumExperienceYears.HasValue && meetsMinimumExperience)
            {
                score += 15;
                summary.Add($"Experience matched: {profile.ExperienceYears} years");
            }
            else if (project.MinimumExperienceYears.HasValue)
            {
                summary.Add($"Experience below minimum: {profile.ExperienceYears}/{project.MinimumExperienceYears.Value} years");
            }

            var locationMatched = TextMatches(project.Location, profile.Location);
            if (!string.IsNullOrWhiteSpace(project.Location) && locationMatched)
            {
                score += 4;
                summary.Add($"Location matched: {profile.Location}");
            }

            var educationMatched = TextMatches(project.PreferredEducation, profile.HighestEducation);
            if (!string.IsNullOrWhiteSpace(project.PreferredEducation) && educationMatched)
            {
                score += 3;
                summary.Add($"Education matched: {profile.HighestEducation}");
            }

            if (score <= 0)
            {
                continue;
            }

            results.Add(new CompanyProjectCandidateMatch(
                record.Id,
                string.IsNullOrWhiteSpace(profile.Name) ? "Unknown candidate" : profile.Name,
                GetCurrentRole(profile),
                profile.ExperienceYears,
                Math.Min(score, 100),
                roleMatchType,
                matchedRequiredCertifications,
                missingRequiredCertifications,
                matchedPreferredCertifications,
                matchedRequiredSkills,
                missingRequiredSkills,
                matchedPreferredSkills,
                meetsMinimumExperience,
                locationMatched,
                educationMatched,
                summary));
        }

        return results
            .OrderByDescending(match => match.MatchScore)
            .ThenByDescending(match => match.ExperienceYears)
            .ThenBy(match => match.CandidateName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> CollectCandidateRoles(StoredCandidateProfile profile)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in profile.JobTitles)
        {
            AddRole(roles, role);
        }

        foreach (var role in profile.RoleExperience.Select(item => item.JobTitle))
        {
            AddRole(roles, role);
        }

        foreach (var experience in profile.Experiences)
        {
            AddRole(roles, !string.IsNullOrWhiteSpace(experience.StandardRoleName) ? experience.StandardRoleName : experience.RawRoleTitle);
        }

        return roles;
    }

    private static void AddRole(HashSet<string> roles, string? raw)
    {
        var normalized = Normalize(raw);
        if (normalized.Length > 0)
        {
            roles.Add(normalized);
        }
    }

    private static string GetCurrentRole(StoredCandidateProfile profile)
    {
        var fromRoleExperience = profile.RoleExperience
            .OrderByDescending(item => item.Years)
            .Select(item => item.JobTitle)
            .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));

        if (!string.IsNullOrWhiteSpace(fromRoleExperience))
        {
            return fromRoleExperience;
        }

        var firstJobTitle = profile.JobTitles.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));
        return string.IsNullOrWhiteSpace(firstJobTitle) ? "N/A" : firstJobTitle;
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<string> MatchTerms(IReadOnlyList<string> expectedTerms, IReadOnlyList<string>? candidateTerms)
    {
        if (expectedTerms.Count == 0 || candidateTerms is null || candidateTerms.Count == 0)
        {
            return [];
        }

        var normalizedCandidateTerms = candidateTerms
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();

        return expectedTerms
            .Where(expected => normalizedCandidateTerms.Any(candidate => TextMatches(expected, candidate)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExceptTerms(IReadOnlyList<string> source, IReadOnlyList<string> matched)
    {
        return source
            .Where(item => matched.All(other => !string.Equals(item, other, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static bool TextMatches(string? leftValue, string? rightValue)
    {
        var left = Normalize(leftValue);
        var right = Normalize(rightValue);
        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        return left == right ||
               left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
               right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return RoleCatalogSeeder.Normalize(value);
    }
}
