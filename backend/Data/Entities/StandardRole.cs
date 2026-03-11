namespace RigMatch.Api.Data.Entities;

public class StandardRole
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<RoleAlias> Aliases { get; set; } = new List<RoleAlias>();

    public ICollection<SuggestedRoleAlias> SuggestedAliases { get; set; } = new List<SuggestedRoleAlias>();
}
