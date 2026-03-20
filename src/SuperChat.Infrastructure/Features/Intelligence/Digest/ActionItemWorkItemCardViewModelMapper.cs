using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal static class ActionItemWorkItemCardViewModelMapper
{
    public static ActionItemWorkItemCardViewModel Map(
        WorkItemRecord item,
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
            Id = item.Id
        };
    }
}
