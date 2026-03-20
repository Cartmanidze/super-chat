using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal static class RequestWorkItemCardViewModelMapper
{
    public static RequestWorkItemCardViewModel Map(
        WorkItemRecord item,
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
            Id = item.Id
        };
    }
}
