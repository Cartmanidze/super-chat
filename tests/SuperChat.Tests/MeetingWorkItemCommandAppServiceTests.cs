using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SuperChat.Application.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Shared.Persistence;
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

    [Fact]
    public async Task ConfirmAsync_ChangesMeetingStatusToConfirmed()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var repository = new EfMeetingRepository(factory);
        var userId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 04, 08, 11, 30, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Interview",
                Summary = "Candidate interview today at 18:00",
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = "$evt-confirm",
                ObservedAt = now.AddHours(-1),
                ScheduledFor = now.AddHours(2),
                Confidence = 0.91,
                Status = MeetingStatus.PendingConfirmation,
                CreatedAt = now.AddHours(-1),
                UpdatedAt = now.AddHours(-1)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        dynamic service = new MeetingWorkItemCommandAppService(repository, new FakeTimeProvider(now));

        var result = (bool)await service.ConfirmAsync(userId, meetingId, CancellationToken.None);

        Assert.True(result);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.Equal(MeetingStatus.Confirmed, meeting.Status);
        Assert.Equal(now, meeting.UpdatedAt);
        Assert.Null(meeting.ResolvedAt);
        Assert.Null(meeting.ResolutionKind);
    }

    [Fact]
    public async Task UnconfirmAsync_ChangesMeetingStatusToPendingConfirmation()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var repository = new EfMeetingRepository(factory);
        var userId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 04, 08, 11, 45, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meetingId,
                UserId = userId,
                Title = "Interview",
                Summary = "Candidate interview today at 18:00",
                SourceRoom = "!team:matrix.localhost",
                SourceEventId = "$evt-unconfirm",
                ObservedAt = now.AddHours(-2),
                ScheduledFor = now.AddHours(2),
                Confidence = 0.91,
                Status = MeetingStatus.Confirmed,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-2)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        dynamic service = new MeetingWorkItemCommandAppService(repository, new FakeTimeProvider(now));

        var result = (bool)await service.UnconfirmAsync(userId, meetingId, CancellationToken.None);

        Assert.True(result);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meeting = await verificationContext.Meetings.SingleAsync(item => item.Id == meetingId, CancellationToken.None);
        Assert.Equal(MeetingStatus.PendingConfirmation, meeting.Status);
        Assert.Equal(now, meeting.UpdatedAt);
        Assert.Null(meeting.ResolvedAt);
        Assert.Null(meeting.ResolutionKind);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsFalse_WhenMeetingDoesNotExist()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var repository = new EfMeetingRepository(factory);
        var userId = Guid.NewGuid();
        dynamic service = new MeetingWorkItemCommandAppService(
            repository,
            new FakeTimeProvider(new DateTimeOffset(2026, 04, 08, 12, 00, 00, TimeSpan.Zero)));

        var result = (bool)await service.ConfirmAsync(userId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-meeting-command-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private sealed class TestDbContextFactory(DbContextOptions<SuperChatDbContext> options) : IDbContextFactory<SuperChatDbContext>
    {
        public SuperChatDbContext CreateDbContext()
        {
            return new SuperChatDbContext(options);
        }

        public Task<SuperChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SuperChatDbContext(options));
        }
    }
}
