namespace SuperChat.Domain.Features.Intelligence;

public static class HeuristicSignalDetector
{
    public static IReadOnlyCollection<ExtractedItem> Detect(ConversationWindow window, TimeZoneInfo referenceTimeZone)
    {
        if (window.Messages.Count == 0)
        {
            return Array.Empty<ExtractedItem>();
        }

        var items = new List<ExtractedItem>();
        AddMeetingItems(window, referenceTimeZone, items);
        return items;
    }

    private static void AddMeetingItems(
        ConversationWindow window,
        TimeZoneInfo referenceTimeZone,
        List<ExtractedItem> items)
    {
        var transcript = window.Transcript.Trim();
        if (string.IsNullOrWhiteSpace(transcript) || StructuredArtifactDetector.LooksLikeStructuredArtifact(transcript))
        {
            return;
        }

        var meetingSignal = window.Messages.Count == 1
            ? MeetingSignalDetector.TryFromMessage(window.LastMessage, referenceTimeZone)
            : MeetingSignalDetector.TryFromChunk(
                transcript,
                window.TsFrom,
                window.TsTo,
                referenceTimeZone);

        if (meetingSignal is null)
        {
            return;
        }

        items.Add(new ExtractedItem(
            Guid.NewGuid(),
            window.UserId,
            ExtractedItemKind.Meeting,
            "Скоро встреча",
            meetingSignal.Summary,
            window.ExternalChatId,
            window.LastMessage.ExternalMessageId,
            meetingSignal.Person,
            window.TsTo,
            meetingSignal.ScheduledFor,
            meetingSignal.Confidence));
    }
}
