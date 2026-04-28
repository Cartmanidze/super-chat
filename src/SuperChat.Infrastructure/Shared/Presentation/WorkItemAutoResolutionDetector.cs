using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Shared.Presentation;

internal static class WorkItemAutoResolutionDetector
{
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
        MeetingEntity item,
        IReadOnlyList<ChatMessageEntity> laterMessages)
    {
        if (laterMessages.Count == 0)
        {
            return null;
        }

        return item.ScheduledFor is DateTimeOffset scheduledFor
            ? TryResolveMeeting(scheduledFor, laterMessages)
            : null;
    }

    private static WorkItemAutoResolution? TryResolveMeeting(
        DateTimeOffset scheduledFor,
        IReadOnlyList<ChatMessageEntity> laterMessages)
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

    private static bool LooksMeaningful(ChatMessageEntity message)
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
