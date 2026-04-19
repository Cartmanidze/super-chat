namespace SuperChat.Contracts.Features.Integrations.Max;

public sealed class MaxUserbotOptions
{
    public const string SectionName = "MaxUserbot";

    public bool Enabled { get; set; } = false;

    public string BaseUrl { get; set; } = "http://max-userbot-service:7591";

    public string HmacSecret { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
