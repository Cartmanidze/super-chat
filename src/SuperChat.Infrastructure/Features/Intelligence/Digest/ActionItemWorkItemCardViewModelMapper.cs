using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class ActionItemWorkItemCardViewModelMapper
{
    public static ActionItemWorkItemCardViewModel Map(
        ExtractedItem item,
        WorkItemMetadata metadata)
    {
        return new ActionItemWorkItemCardViewModel(
            item.Title,
            item.Summary,
            item.Kind.ToString(),
            item.ObservedAt,
            item.DueAt,
            item.SourceRoom,
            metadata.Status.ToActionItemStatus() ?? ActionItemStatus.ToDo,
            item.Confidence,
            metadata.Priority,
            metadata.Owner,
            metadata.Origin ?? WorkItemOrigin.DetectedFromChat,
            metadata.ReviewState,
            metadata.PlannedAt,
            metadata.Source,
            metadata.UpdatedAt,
            metadata.IsOverdue)
        {
            ActionKey = WorkItemActionKey.ForExtractedItem(item.Id)
        };
    }
}
