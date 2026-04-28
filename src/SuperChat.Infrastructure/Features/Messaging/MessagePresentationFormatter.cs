namespace SuperChat.Infrastructure.Features.Messaging;

internal static class MessagePresentationFormatter
{
    private const string TelegramSuffix = " (Telegram)";

    internal static string ResolveDisplaySenderName(string senderName, string? chatTitle)
    {
        if (LooksLikeHumanReadableSenderName(senderName))
        {
            return senderName;
        }

        if (!string.IsNullOrWhiteSpace(chatTitle))
        {
            return StripTelegramSuffix(chatTitle);
        }

        return senderName;
    }

    private static bool LooksLikeHumanReadableSenderName(string senderName)
    {
        if (string.IsNullOrWhiteSpace(senderName) ||
            string.Equals(senderName, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(senderName, "You", StringComparison.Ordinal))
        {
            return true;
        }

        var trimmed = senderName.Trim();
        return trimmed.Length < 6 || !trimmed.All(char.IsDigit);
    }

    private static string StripTelegramSuffix(string chatTitle)
    {
        return chatTitle.EndsWith(TelegramSuffix, StringComparison.Ordinal)
            ? chatTitle[..^TelegramSuffix.Length]
            : chatTitle;
    }
}
