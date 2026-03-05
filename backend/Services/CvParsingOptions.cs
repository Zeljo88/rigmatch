namespace RigMatch.Api.Services;

public sealed class CvParsingOptions
{
    public bool UseMockInDevelopment { get; set; } = true;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-10-21";
    public int MaxTextChars { get; set; } = 40_000;
}
