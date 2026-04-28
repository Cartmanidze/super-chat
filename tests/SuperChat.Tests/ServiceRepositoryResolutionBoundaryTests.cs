using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class ServiceRepositoryResolutionBoundaryTests
{
    [Fact]
    public void MeetingService_UsesMeetingRepositoryBoundaryForManualResolution()
    {
        var constructor = typeof(MeetingService).GetConstructor(
        [
            typeof(MeetingUpsertService),
            typeof(IMeetingRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);
        AssertNoManualResolutionDependency<MeetingService>("MeetingManualResolutionService");
    }

    [Fact]
    public async Task MeetingService_DismissAsync_UsesMeetingRepositoryForRelatedResolution()
    {
        var repository = Substitute.For<IMeetingRepository>();
        var userId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 17, 10, 15, 00, TimeSpan.Zero);

        repository.FindByIdAsync(userId, meetingId, CancellationToken.None)
            .Returns(CreateMeetingRecord(userId, "$meeting-shared"));

        var service = CreateMeetingService(repository, new FixedTimeProvider(now));

        var result = await service.DismissAsync(userId, meetingId, CancellationToken.None);

        Assert.True(result);
        await repository.Received(1).FindByIdAsync(userId, meetingId, CancellationToken.None);
        await repository.Received(1).ResolveRelatedAsync(
            userId,
            "$meeting-shared",
            WorkItemResolutionState.Dismissed,
            WorkItemResolutionState.Manual,
            now,
            CancellationToken.None);
    }

    [Fact]
    public async Task MeetingService_CompleteAsync_ReturnsFalse_WhenMeetingIsMissing()
    {
        var repository = Substitute.For<IMeetingRepository>();
        var userId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 17, 10, 15, 00, TimeSpan.Zero);

        repository.FindByIdAsync(userId, meetingId, CancellationToken.None)
            .Returns((MeetingRecord?)null);

        var service = CreateMeetingService(repository, new FixedTimeProvider(now));

        var result = await service.CompleteAsync(userId, meetingId, CancellationToken.None);

        Assert.False(result);
        await repository.Received(1).FindByIdAsync(userId, meetingId, CancellationToken.None);
        await repository.DidNotReceive().ResolveRelatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WorkItemService_UsesWorkItemRepositoryBoundaryForManualResolution()
    {
        var constructor = typeof(WorkItemService).GetConstructor(
        [
            typeof(WorkItemWriter),
            typeof(IWorkItemRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);
        AssertNoManualResolutionDependency<WorkItemService>("WorkItemManualResolutionService");
    }

    [Fact]
    public async Task WorkItemService_CompleteAsync_UsesWorkItemRepositoryForRelatedResolution()
    {
        var repository = Substitute.For<IWorkItemRepository>();
        var userId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 17, 11, 20, 00, TimeSpan.Zero);

        repository.FindByIdAsync(userId, workItemId, CancellationToken.None)
            .Returns(CreateWorkItemRecord(userId, "$work-item-shared"));

        var service = CreateWorkItemService(repository, new FixedTimeProvider(now));

        var result = await service.CompleteAsync(userId, workItemId, CancellationToken.None);

        Assert.True(result);
        await repository.Received(1).FindByIdAsync(userId, workItemId, CancellationToken.None);
        await repository.Received(1).ResolveRelatedAsync(
            userId,
            "$work-item-shared",
            WorkItemResolutionState.Completed,
            WorkItemResolutionState.Manual,
            now,
            CancellationToken.None);
    }

    [Fact]
    public async Task WorkItemService_DismissAsync_ReturnsFalse_WhenWorkItemIsMissing()
    {
        var repository = Substitute.For<IWorkItemRepository>();
        var userId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 17, 11, 20, 00, TimeSpan.Zero);

        repository.FindByIdAsync(userId, workItemId, CancellationToken.None)
            .Returns((WorkItemRecord?)null);

        var service = CreateWorkItemService(repository, new FixedTimeProvider(now));

        var result = await service.DismissAsync(userId, workItemId, CancellationToken.None);

        Assert.False(result);
        await repository.Received(1).FindByIdAsync(userId, workItemId, CancellationToken.None);
        await repository.DidNotReceive().ResolveRelatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    private static MeetingService CreateMeetingService(IMeetingRepository repository, TimeProvider timeProvider)
    {
        var constructor = typeof(MeetingService).GetConstructor(
        [
            typeof(MeetingUpsertService),
            typeof(IMeetingRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);

        return (MeetingService)constructor.Invoke(
        [
            new MeetingUpsertService(
                Substitute.For<IDbContextFactory<SuperChatDbContext>>(),
                NullLogger<MeetingUpsertService>.Instance),
            repository,
            timeProvider
        ]);
    }

    private static WorkItemService CreateWorkItemService(IWorkItemRepository repository, TimeProvider timeProvider)
    {
        var constructor = typeof(WorkItemService).GetConstructor(
        [
            typeof(WorkItemWriter),
            typeof(IWorkItemRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);

        return (WorkItemService)constructor.Invoke(
        [
            new WorkItemWriter(
                Substitute.For<IMeetingService>(),
                NullLogger<WorkItemWriter>.Instance),
            repository,
            timeProvider
        ]);
    }

    private static void AssertNoManualResolutionDependency<TService>(string dependencyTypeName)
    {
        Assert.DoesNotContain(
            typeof(TService).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => string.Equals(parameter.ParameterType.Name, dependencyTypeName, StringComparison.Ordinal));
    }

    private static MeetingRecord CreateMeetingRecord(Guid userId, string sourceEventId)
    {
        return new MeetingRecord(
            Guid.NewGuid(),
            userId,
            "Sync with product",
            "Walk through the latest status update.",
            "!team:matrix.localhost",
            sourceEventId,
            null,
            new DateTimeOffset(2026, 03, 13, 15, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero),
            new Confidence(0.91));
    }

    private static WorkItemRecord CreateWorkItemRecord(Guid userId, string sourceEventId)
    {
        return new WorkItemRecord(
            Guid.NewGuid(),
            userId,
            ExtractedItemKind.Meeting,
            "Send contract",
            "Please send the contract tomorrow.",
            "!sales:matrix.localhost",
            sourceEventId,
            null,
            new DateTimeOffset(2026, 03, 13, 15, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 14, 09, 00, 00, TimeSpan.Zero),
            new Confidence(0.87));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
