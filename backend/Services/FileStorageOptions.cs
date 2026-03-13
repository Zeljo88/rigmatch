namespace RigMatch.Api.Services;

public sealed class FileStorageOptions
{
    public string Provider { get; set; } = "Local";

    public string LocalRoot { get; set; } = "uploads";

    public string ConnectionString { get; set; } = string.Empty;

    public string AccountUrl { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "cvs";
}
