namespace SuperChat.Contracts.Features.Integrations.Telegram;

public sealed class TelegramUserbotOptions
{
    public const string SectionName = "TelegramUserbot";

    public bool Enabled { get; set; } = false;

    public string BaseUrl { get; set; } = "http://telegram-userbot-service:7491";

    public string HmacSecret { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
