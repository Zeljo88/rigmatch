namespace RigMatch.Api.Services;

public interface IRoleStandardizationService
{
    Task<RoleMatchResult> MatchRoleAsync(string? rawRole, CancellationToken cancellationToken = default);

    Task<string> StandardizeRoleAsync(string? rawRole, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> StandardizeRoleListAsync(IEnumerable<string>? rawRoles, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetStandardRolesAsync(CancellationToken cancellationToken = default);
}

public sealed record RoleMatchResult(
    string RawRoleTitle,
    int? StandardRoleId,
    string StandardRoleName,
    double MatchConfidence,
    bool NeedsReview);
