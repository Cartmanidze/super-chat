using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Domain.Features.Intelligence;

public static class WaitingOnTurnDetector
{
    public static ChatMessage? GetLatestMeaningfulMessage(ConversationWindow window)
    {
        for (var index = window.Messages.Count - 1; index >= 0; index--)
        {
            var message = window.Messages[index];
            if (LooksMeaningful(message))
            {
                return message;
            }
        }

        return null;
    }

    public static ChatMessage? GetUnansweredExternalMessage(ConversationWindow window)
    {
        var latestMeaningfulMessage = GetLatestMeaningfulMessage(window);
        if (latestMeaningfulMessage is null)
        {
            return null;
        }

        return IsOwnMessage(latestMeaningfulMessage)
            ? null
            : latestMeaningfulMessage;
    }

    public static bool HasUnansweredExternalTurn(ConversationWindow window)
    {
        return GetUnansweredExternalMessage(window) is not null;
    }

    public static bool IsOwnMessage(ChatMessage message)
    {
        // Primary: persisted outgoing flag from the userbot sidecar.
        // Fallback: legacy heuristic on sender name "You" — оставлено для совместимости
        // со старыми сообщениями из БД, у которых is_outgoing ещё не проставлен.
        return message.IsOutgoing || IsOwnSender(message.SenderName);
    }

    public static bool IsOwnSender(string? senderName)
    {
        return string.Equals(senderName?.Trim(), "You", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksMeaningful(ChatMessage message)
    {
        return !string.IsNullOrWhiteSpace(message.Text);
    }
}
