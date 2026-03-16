using SuperChat.Domain.Model;

namespace SuperChat.Domain.Services;

public static class WaitingOnTurnDetector
{
    public static NormalizedMessage? GetLatestMeaningfulMessage(ConversationWindow window)
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

    public static NormalizedMessage? GetUnansweredExternalMessage(ConversationWindow window)
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

    public static bool IsOwnMessage(NormalizedMessage message)
    {
        return string.Equals(message.SenderName?.Trim(), "You", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksMeaningful(NormalizedMessage message)
    {
        return !string.IsNullOrWhiteSpace(message.Text);
    }
}
