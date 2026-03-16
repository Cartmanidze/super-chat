using SuperChat.Domain.Model;

namespace SuperChat.Domain.Services;

public static class WaitingOnTurnDetector
{
    public static NormalizedMessage? GetUnansweredExternalMessage(ConversationWindow window)
    {
        for (var index = window.Messages.Count - 1; index >= 0; index--)
        {
            var message = window.Messages[index];
            if (!LooksMeaningful(message))
            {
                continue;
            }

            return IsOwnMessage(message) ? null : message;
        }

        return null;
    }

    public static bool HasUnansweredExternalTurn(ConversationWindow window)
    {
        return GetUnansweredExternalMessage(window) is not null;
    }

    public static bool IsOwnMessage(NormalizedMessage message)
    {
        return string.Equals(message.SenderName?.Trim(), "You", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksMeaningful(NormalizedMessage message)
    {
        return !string.IsNullOrWhiteSpace(message.Text);
    }
}
