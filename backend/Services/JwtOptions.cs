namespace RigMatch.Api.Services;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "RigMatch";

    public string Audience { get; set; } = "RigMatch.Frontend";

    public string SigningKey { get; set; } = "RigMatch-Development-Signing-Key-Change-Me-12345";

    public int LifetimeMinutes { get; set; } = 480;
}
