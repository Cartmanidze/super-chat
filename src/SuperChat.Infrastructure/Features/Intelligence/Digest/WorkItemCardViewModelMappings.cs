using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal static class WorkItemCardViewModelMappings
{
    public static WorkItemCardViewModel ToWorkItemCardViewModel(this MeetingRecord meeting, DateTimeOffset now)
    {
        var metadata = WorkItemPresentationMetadata.FromMeeting(meeting, now);
        return MeetingWorkItemCardViewModelMapper.Map(meeting, metadata);
    }

    public static WorkItemCardViewModel WithResolvedChatTitle(
        this WorkItemCardViewModel card,
        IReadOnlyDictionary<string, string> chatTitles)
    {
        if (chatTitles.TryGetValue(card.ChatTitle, out var resolvedTitle))
        {
            return card with { ChatTitle = resolvedTitle };
        }

        // Если читаемого имени нет, а текущее значение это сырой идентификатор чата
        // (числовой Telegram chat id или legacy Matrix room id), не отдаём его в UI.
        return ChatTitleHeuristics.LooksLikeRawChatId(card.ChatTitle)
            ? card with { ChatTitle = string.Empty }
            : card;
    }
}
