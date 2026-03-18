using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class EventWorkItemTypeStrategy(
    IExtractedItemService extractedItemService,
    IMeetingService meetingService,
    ExtractedItemLookupService extractedItemLookupService,
    MeetingLookupService meetingLookupService) : IWorkItemTypeStrategy
{
    public WorkItemType Type => WorkItemType.Event;

    public IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot)
    {
        var meetingSourceEventIds = snapshot.Meetings
            .Select(item => item.SourceEventId)
            .ToHashSet(StringComparer.Ordinal);

        var meetingCards = snapshot.Meetings
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Select(item => item.ToWorkItemCardViewModel(snapshot.Now).WithResolvedSourceRoom(snapshot.RoomNames));

        var extractedFallbackCards = snapshot.ExtractedItems
            .Where(IsEvent)
            .Where(item => !meetingSourceEventIds.Contains(item.SourceEventId))
            .OrderBy(item => item.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.Confidence)
            .ThenByDescending(item => item.ObservedAt)
            .Select(item => item.ToWorkItemCardViewModel(snapshot.Now).WithResolvedSourceRoom(snapshot.RoomNames));

        return meetingCards
            .Concat(extractedFallbackCards)
            .ToList();
    }

    public async Task<bool> CompleteAsync(Guid userId, string actionKey, CancellationToken cancellationToken)
    {
        return await ResolveAsync(userId, actionKey, isComplete: true, cancellationToken);
    }

    public async Task<bool> DismissAsync(Guid userId, string actionKey, CancellationToken cancellationToken)
    {
        return await ResolveAsync(userId, actionKey, isComplete: false, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        string actionKey,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        if (!WorkItemActionKey.TryParse(actionKey, out var target, out var id))
        {
            return false;
        }

        return target switch
        {
            WorkItemActionTarget.ExtractedItem => await ResolveExtractedItemAsync(userId, id, isComplete, cancellationToken),
            WorkItemActionTarget.Meeting => await ResolveMeetingAsync(userId, id, isComplete, cancellationToken),
            _ => false
        };
    }

    private async Task<bool> ResolveExtractedItemAsync(
        Guid userId,
        Guid itemId,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        var item = await extractedItemLookupService.GetByIdAsync(userId, itemId, cancellationToken);
        if (item is null || !IsEvent(item))
        {
            return false;
        }

        return isComplete
            ? await extractedItemService.CompleteAsync(userId, itemId, cancellationToken)
            : await extractedItemService.DismissAsync(userId, itemId, cancellationToken);
    }

    private async Task<bool> ResolveMeetingAsync(
        Guid userId,
        Guid meetingId,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        var meeting = await meetingLookupService.GetByIdAsync(userId, meetingId, cancellationToken);
        if (meeting is null)
        {
            return false;
        }

        return isComplete
            ? await meetingService.CompleteAsync(userId, meetingId, cancellationToken)
            : await meetingService.DismissAsync(userId, meetingId, cancellationToken);
    }

    private static bool IsEvent(SuperChat.Domain.Model.ExtractedItem item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.Event;
    }
}
