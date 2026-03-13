namespace RigMatch.Api.Models;

public sealed class RegisterEmployerRequest
{
    public string? CompanyName { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }
}
