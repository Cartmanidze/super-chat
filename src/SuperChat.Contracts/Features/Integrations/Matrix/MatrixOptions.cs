namespace SuperChat.Contracts.Configuration;

public sealed class MatrixOptions
{
    public const string SectionName = "Matrix";

    public string HomeserverUrl { get; set; } = "https://matrix.localhost";

    public string AdminAccessToken { get; set; } = string.Empty;

    public string UserIdPrefix { get; set; } = "superchat";
}
