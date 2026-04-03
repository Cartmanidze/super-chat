using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class WorkItemPresentationMetadataTests
{
    [Theory]
    [InlineData(ExtractedItemKind.WaitingOn, WorkItemType.Request, WorkItemOwner.Contact, WorkItemOrigin.Request)]
    [InlineData(ExtractedItemKind.Meeting, WorkItemType.Meeting, WorkItemOwner.Both, WorkItemOrigin.DetectedFromChat)]
    [InlineData(ExtractedItemKind.Task, WorkItemType.ActionItem, WorkItemOwner.Me, WorkItemOrigin.DetectedFromChat)]
    [InlineData(ExtractedItemKind.Commitment, WorkItemType.ActionItem, WorkItemOwner.Me, WorkItemOrigin.Promise)]
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

    [Fact]
    public void LegacyStringResolvers_RemainCompatibleDuringMigration()
    {
        Assert.Equal(
            WorkItemPresentationMetadata.ResolveType(ExtractedItemKind.Task),
            WorkItemPresentationMetadata.ResolveType("Task"));
        Assert.Equal(
            WorkItemPresentationMetadata.ResolveStatus(ExtractedItemKind.WaitingOn, "Need an answer"),
            WorkItemPresentationMetadata.ResolveStatus("WaitingOn", "Need an answer"));
    }
}
