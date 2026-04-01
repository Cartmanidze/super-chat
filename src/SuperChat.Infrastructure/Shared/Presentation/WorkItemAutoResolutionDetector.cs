using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Shared.Presentation;

internal static class WorkItemAutoResolutionDetector
{
    private static readonly string[] CompletionKeywords =
    [
        "готово",
        "сделал",
        "сделано",
        "сделали",
        "выполнено",
        "выполнил",
        "выполнила",
        "отправил",
        "отправила",
        "отправлено",
        "прислал",
        "прислала",
        "выслал",
        "выслала",
        "закрыл",
        "закрыла",
        "закрыто",
        "завершил",
        "завершила",
        "finished",
        "done",
        "completed",
        "sent",
        "shipped",
        "resolved"
    ];

    private static readonly string[] AcknowledgementKeywords =
    [
        "спасибо, получил",
        "спасибо получила",
        "получил",
        "получила",
        "вижу",
        "принято",
        "thanks, got it",
        "thanks got it",
        "got it",
        "received",
        "looks good"
    ];

    private static readonly string[] MeetingCompletionKeywords =
    [
        "спасибо за встречу",
        "спасибо за созвон",
        "после созвона",
        "по итогам созвона",
        "по итогам встречи",
        "после встречи",
        "созвонились",
        "встретились",
        "обсудили на созвоне",
        "обсудили на встрече",
        "thanks for the call",
        "thanks for the meeting",
        "after the call",
        "after the meeting",
        "great meeting"
    ];

    public static WorkItemAutoResolution? TryResolve(
        ExtractedItemEntity item,
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        if (laterMessages.Count == 0)
        {
            return null;
        }

        return item.Kind switch
        {
            ExtractedItemKind.WaitingOn => TryResolveWaiting(item, laterMessages),
            ExtractedItemKind.Task or ExtractedItemKind.Commitment => TryResolveActionItem(laterMessages),
            ExtractedItemKind.Meeting => TryResolveMeeting(item.DueAt ?? item.ObservedAt, laterMessages),
            _ => null
        };
    }

    public static WorkItemAutoResolution? TryResolve(
        WorkItemEntity item,
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        if (laterMessages.Count == 0)
        {
            return null;
        }

        return item.Kind switch
        {
            ExtractedItemKind.WaitingOn => TryResolveWaiting(item.SourceEventId, laterMessages),
            ExtractedItemKind.Task or ExtractedItemKind.Commitment => TryResolveActionItem(laterMessages),
            ExtractedItemKind.Meeting => TryResolveMeeting(item.DueAt ?? item.ObservedAt, laterMessages),
            _ => null
        };
    }

    public static WorkItemAutoResolution? TryResolve(
        MeetingEntity item,
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        if (laterMessages.Count == 0)
        {
            return null;
        }

        return TryResolveMeeting(item.ScheduledFor, laterMessages);
    }

    private static WorkItemAutoResolution? TryResolveWaiting(
        ExtractedItemEntity item,
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        return TryResolveWaiting(item.SourceEventId, laterMessages);
    }

    private static WorkItemAutoResolution? TryResolveWaiting(
        string sourceEventId,
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        var reply = laterMessages.FirstOrDefault(message =>
            message.MatrixEventId != sourceEventId &&
            LooksMeaningful(message) &&
            WaitingOnTurnDetector.IsOwnSender(message.SenderName));

        return reply is null
            ? null
            : new WorkItemAutoResolution(
                reply.SentAt,
                WorkItemResolutionState.Completed,
                WorkItemResolutionState.AutoReply);
    }

    private static WorkItemAutoResolution? TryResolveActionItem(
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        foreach (var message in laterMessages)
        {
            if (!LooksMeaningful(message))
            {
                continue;
            }

            var lowered = message.Text.Trim().ToLowerInvariant();
            if (ContainsAny(lowered, CompletionKeywords) || ContainsAny(lowered, AcknowledgementKeywords))
            {
                return new WorkItemAutoResolution(
                    message.SentAt,
                    WorkItemResolutionState.Completed,
                    WorkItemResolutionState.AutoCompletion);
            }
        }

        return null;
    }

    private static WorkItemAutoResolution? TryResolveMeeting(
        DateTimeOffset scheduledFor,
        IReadOnlyList<NormalizedMessageEntity> laterMessages)
    {
        foreach (var message in laterMessages)
        {
            if (message.SentAt < scheduledFor)
            {
                continue;
            }

            if (!LooksMeaningful(message))
            {
                continue;
            }

            var lowered = message.Text.Trim().ToLowerInvariant();
            if (ContainsAny(lowered, MeetingCompletionKeywords))
            {
                return new WorkItemAutoResolution(
                    message.SentAt,
                    WorkItemResolutionState.Completed,
                    WorkItemResolutionState.AutoMeetingCompletion);
            }
        }

        return null;
    }

    private static bool LooksMeaningful(NormalizedMessageEntity message)
    {
        return !string.IsNullOrWhiteSpace(message.Text) &&
               !StructuredArtifactDetector.LooksLikeStructuredArtifact(message.Text);
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }
}

internal sealed record WorkItemAutoResolution(
    DateTimeOffset ResolvedAt,
    string ResolutionKind,
    string ResolutionSource);
