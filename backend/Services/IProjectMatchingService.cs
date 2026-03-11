using RigMatch.Api.Data.Entities;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public interface IProjectMatchingService
{
    IReadOnlyList<CompanyProjectCandidateMatch> MatchCandidates(
        CompanyProject project,
        IReadOnlyList<CvRecord> finalizedCandidates);
}
