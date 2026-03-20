using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Domain.Features.Intelligence;

public static class ConversationWindowSettlement
{
    public static readonly TimeSpan DialogueGap = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(20);

    public static IReadOnlyList<ConversationWindow> BuildReadyConversationWindows(
        IReadOnlyList<NormalizedMessage> pendingMessages,
        DateTimeOffset now)
    {
        if (pendingMessages.Count == 0)
        {
            return [];
        }

        var windows = new List<ConversationWindow>();
        foreach (var roomGroup in pendingMessages
                     .GroupBy(message => new { message.UserId, message.Source, message.MatrixRoomId }))
        {
            var orderedMessages = roomGroup
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.IngestedAt)
                .ThenBy(message => message.Id)
                .ToList();

            var buffer = new List<NormalizedMessage>();
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

    private static void TryAddWindow(
        IReadOnlyList<NormalizedMessage> messages,
        DateTimeOffset now,
        ICollection<ConversationWindow> windows)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var lastMessage = messages[^1];
        if (now - lastMessage.IngestedAt < SettleDelay)
        {
            return;
        }

        windows.Add(new ConversationWindow(
            lastMessage.UserId,
            lastMessage.Source,
            lastMessage.MatrixRoomId,
            messages.ToList()));
    }
}
