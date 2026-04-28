using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Domain.Features.Intelligence;

public static class ConversationWindowSettlement
{
    public static readonly TimeSpan DialogueGap = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(20);

    public static IReadOnlyList<ConversationWindow> BuildReadyConversationWindows(
        IReadOnlyList<ChatMessage> pendingMessages,
        DateTimeOffset now)
    {
        if (pendingMessages.Count == 0)
        {
            return [];
        }

        var windows = new List<ConversationWindow>();
        foreach (var roomGroup in pendingMessages
                     .GroupBy(message => new { message.UserId, message.Source, message.ExternalChatId }))
        {
            var orderedMessages = roomGroup
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.ReceivedAt)
                .ThenBy(message => message.Id)
                .ToList();

            var buffer = new List<ChatMessage>();
            foreach (var message in orderedMessages)
            {
                if (buffer.Count > 0)
                {
                    var previous = buffer[^1];
                    if (message.SentAt - previous.SentAt > DialogueGap)
                    {
                        TryAddWindow(buffer, now, windows);
                        buffer.Clear();
                    }
                }

                buffer.Add(message);
            }

            TryAddWindow(buffer, now, windows);
        }

        return windows;
    }

    public static TimeSpan? GetNextRetryDelay(
        IReadOnlyList<ChatMessage> pendingMessages,
        DateTimeOffset now)
    {
        if (pendingMessages.Count == 0)
        {
            return null;
        }

        TimeSpan? nextDelay = null;
        foreach (var roomGroup in pendingMessages
                     .GroupBy(message => new { message.UserId, message.Source, message.ExternalChatId }))
        {
            var orderedMessages = roomGroup
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.ReceivedAt)
                .ThenBy(message => message.Id)
                .ToList();

            var buffer = new List<ChatMessage>();
            foreach (var message in orderedMessages)
            {
                if (buffer.Count > 0)
                {
                    var previous = buffer[^1];
                    if (message.SentAt - previous.SentAt > DialogueGap)
                    {
                        TryUpdateNextDelay(buffer, now, ref nextDelay);
                        buffer.Clear();
                    }
                }

                buffer.Add(message);
            }

            TryUpdateNextDelay(buffer, now, ref nextDelay);
        }

        return nextDelay;
    }

    private static void TryAddWindow(
        IReadOnlyList<ChatMessage> messages,
        DateTimeOffset now,
        ICollection<ConversationWindow> windows)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var lastMessage = messages[^1];
        if (now - lastMessage.ReceivedAt < SettleDelay)
        {
            return;
        }

        windows.Add(new ConversationWindow(
            lastMessage.UserId,
            lastMessage.Source,
            lastMessage.ExternalChatId,
            messages.ToList()));
    }

    private static void TryUpdateNextDelay(
        IReadOnlyList<ChatMessage> messages,
        DateTimeOffset now,
        ref TimeSpan? nextDelay)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var remainingDelay = SettleDelay - (now - messages[^1].ReceivedAt);
        if (remainingDelay <= TimeSpan.Zero)
        {
            nextDelay = TimeSpan.Zero;
            return;
        }

        if (nextDelay is null || remainingDelay < nextDelay.Value)
        {
            nextDelay = remainingDelay;
        }
    }
}
