using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class WorkItemPresentationMetadataTests
{
    [Theory]
    [InlineData(ExtractedItemKind.Meeting, WorkItemType.Meeting, WorkItemOwner.Both, WorkItemOrigin.DetectedFromChat)]
    public void TypedResolvers_MapKindToPresentationMetadata(
        ExtractedItemKind kind,
        WorkItemType expectedType,
        WorkItemOwner expectedOwner,
        WorkItemOrigin expectedOrigin)
    {
        Assert.Equal(expectedType, WorkItemPresentationMetadata.ResolveType(kind));
        Assert.Equal(expectedOwner, WorkItemPresentationMetadata.ResolveOwner(kind));
        Assert.Equal(expectedOrigin, WorkItemPresentationMetadata.ResolveOrigin(kind));
    }

    [Fact]
    public void ResolveStatus_TypedMeetingResolverUsesSummaryHeuristics()
    {
        var confirmed = WorkItemPresentationMetadata.ResolveStatus(ExtractedItemKind.Meeting, "confirmed, see you tomorrow");
        var cancelled = WorkItemPresentationMetadata.ResolveStatus(ExtractedItemKind.Meeting, "meeting cancelled");

        Assert.Equal(WorkItemStatus.Confirmed, confirmed);
        Assert.Equal(WorkItemStatus.Cancelled, cancelled);
    }

}
