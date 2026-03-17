namespace SuperChat.Infrastructure.Services;

internal static class MatrixTimelineEventMappings
{
    public static MatrixTimelineEvent? ToMatrixTimelineEvent(this MatrixTimelineEventPayload timelineEvent)
    {
        if (!string.Equals(timelineEvent.Type, "m.room.message", StringComparison.Ordinal))
        {
            return null;
        }

        var body = timelineEvent.Content.GetOptionalStringProperty("body");
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var msgType = timelineEvent.Content.GetOptionalStringProperty("msgtype") ?? "m.text";
        return new MatrixTimelineEvent(
            timelineEvent.EventId ?? string.Empty,
            timelineEvent.Sender ?? string.Empty,
            msgType,
            body,
            ParseTimestamp(timelineEvent.OriginServerTs));
    }

    private static DateTimeOffset ParseTimestamp(long? originServerTs)
    {
        return originServerTs is > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(originServerTs.Value)
            : DateTimeOffset.UtcNow;
    }
}
