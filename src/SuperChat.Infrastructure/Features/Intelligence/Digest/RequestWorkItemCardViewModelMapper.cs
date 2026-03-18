using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class RequestWorkItemCardViewModelMapper
{
    public static RequestWorkItemCardViewModel Map(
        ExtractedItem item,
        WorkItemMetadata metadata)
    {
        return new RequestWorkItemCardViewModel(
            item.Title,
            item.Summary,
            item.Kind.ToString(),
            item.ObservedAt,
            item.DueAt,
            item.SourceRoom,
            metadata.Status.ToRequestStatus() ?? RequestStatus.AwaitingResponse,
            item.Confidence,
            metadata.Priority,
            metadata.Owner,
            metadata.Origin ?? WorkItemOrigin.Request,
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
