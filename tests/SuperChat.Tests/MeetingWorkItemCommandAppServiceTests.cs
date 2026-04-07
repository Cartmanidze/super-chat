using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SuperChat.Application.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class MeetingWorkItemCommandAppServiceTests
{
    [Fact]
    public async Task DismissAsync_ResolvesRelatedMeetingsBySourceEventId()
    {
        var repository = Substitute.For<IMeetingRepository>();
        var userId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 17, 10, 15, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var meeting = new MeetingRecord(
            meetingId,
            userId,
            "Intro call",
            "Call with product team",
            "!team:matrix.localhost",
            "$evt-shared-meeting",
            null,
            now.AddHours(-2),
            now.AddHours(2),
            new Confidence(0.77));

        repository.FindByIdAsync(userId, meetingId, CancellationToken.None).Returns(meeting);

        var service = new MeetingWorkItemCommandAppService(repository, timeProvider);

        var result = await service.DismissAsync(userId, meetingId, CancellationToken.None);

        Assert.True(result);
        await repository.Received(1).ResolveRelatedAsync(
            userId,
            meeting.SourceEventId,
            WorkItemResolutionState.Dismissed,
            WorkItemResolutionState.Manual,
            now,
            CancellationToken.None);
        await repository.DidNotReceive().ResolveAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }
}
