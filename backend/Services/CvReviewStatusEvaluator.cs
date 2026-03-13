using System.Text.Json;
using RigMatch.Api.Data.Entities;

namespace RigMatch.Api.Services;

public static class CvReviewStatusEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CvReviewStatus Evaluate(CvRecord record)
    {
        return Evaluate(record.FinalJson, record.ParsedDraftJson);
    }

    public static CvReviewStatus Evaluate(string? finalJson, string? draftJson)
    {
        var isFinalized = !string.IsNullOrWhiteSpace(finalJson);
        var profileJson = isFinalized ? finalJson : draftJson;
        var hasNeedsReview = ProfileHasNeedsReview(profileJson);
        var reviewStatus = !isFinalized
            ? "draft"
            : hasNeedsReview
                ? "needs-review"
                : "match-ready";

        return new CvReviewStatus(
            isFinalized,
            hasNeedsReview,
            isFinalized && !hasNeedsReview,
            reviewStatus);
    }

    private static bool ProfileHasNeedsReview(string? profileJson)
    {
        if (string.IsNullOrWhiteSpace(profileJson))
        {
            return false;
        }

        try
        {
            var profile = JsonSerializer.Deserialize<ReviewSnapshot>(profileJson, JsonOptions);
            return profile?.Experiences?.Any(experience => experience.NeedsReview) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ReviewSnapshot
    {
        public IReadOnlyList<ReviewSnapshotExperience>? Experiences { get; set; }
    }

    private sealed class ReviewSnapshotExperience
    {
        public bool NeedsReview { get; set; }
    }
}

public sealed record CvReviewStatus(
    bool IsFinalized,
    bool HasNeedsReview,
    bool IsMatchReady,
    string ReviewStatus);
