namespace SuperChat.Contracts.Features.Integrations.Telegram;

public sealed class TelegramBridgeOptions
{
    public const string SectionName = "TelegramBridge";

    public string BotUserId { get; set; } = "@telegrambot:matrix.localhost";

    public string WebLoginBaseUrl { get; set; } = "https://bridge.localhost/public";

    public string ParticipantCountBaseUrl { get; set; } = "http://mautrix-telegram-helper:29318";
}
