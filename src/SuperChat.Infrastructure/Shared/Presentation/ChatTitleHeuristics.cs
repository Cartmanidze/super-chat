namespace SuperChat.Infrastructure.Shared.Presentation;

internal static class ChatTitleHeuristics
{
    /// <summary>
    /// Возвращает true, если строка похожа на необработанный идентификатор чата —
    /// числовой id Telegram (с возможным минусом для групп/каналов) или legacy
    /// Matrix room id вида <c>!abc:server.tld</c>. Используем, чтобы не показывать
    /// такой id в UI, когда читаемое имя ещё не найдено.
    /// </summary>
    public static bool LooksLikeRawChatId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith('!') && trimmed.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        var maybeDigits = trimmed.StartsWith('-') ? trimmed[1..] : trimmed;
        return maybeDigits.Length > 0 && maybeDigits.All(char.IsDigit);
    }
}
