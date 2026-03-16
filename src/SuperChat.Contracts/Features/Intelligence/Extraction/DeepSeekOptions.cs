namespace SuperChat.Contracts.Configuration;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-reasoner";
}
