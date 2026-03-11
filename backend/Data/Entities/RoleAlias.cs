namespace RigMatch.Api.Data.Entities;

public class RoleAlias
{
    public int Id { get; set; }

    public int StandardRoleId { get; set; }

    public StandardRole StandardRole { get; set; } = default!;

    public string Alias { get; set; } = string.Empty;

    public string AliasNormalized { get; set; } = string.Empty;

    public bool RequiresReview { get; set; }
}
