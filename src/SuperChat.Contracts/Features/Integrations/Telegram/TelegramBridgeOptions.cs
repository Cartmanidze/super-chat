namespace SuperChat.Contracts.Configuration;

public sealed class TelegramBridgeOptions
{
    public const string SectionName = "TelegramBridge";

    public string BotUserId { get; set; } = "@telegrambot:matrix.localhost";

    public string WebLoginBaseUrl { get; set; } = "https://bridge.localhost/public";
}
