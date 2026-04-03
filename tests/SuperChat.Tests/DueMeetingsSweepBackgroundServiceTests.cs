using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class DueMeetingsSweepBackgroundServiceTests
{
    [Fact]
    public async Task ResolveDueMeetingsAcrossRooms_AutoResolvesQuietRoomMeeting()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var roomId = "!team:matrix.localhost";
        var observedAt = new DateTimeOffset(2026, 04, 03, 09, 15, 00, TimeSpan.Zero);
        var scheduledFor = new DateTimeOffset(2026, 04, 03, 10, 00, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Daily sync",
                Summary = "Call with Alex",
                SourceRoom = roomId,
                SourceEventId = "$evt-meeting",
                Person = "Alex",
                ObservedAt = observedAt,
                ScheduledFor = scheduledFor,
                Confidence = 0.91,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-after-call",
                SenderName = "Alex",
                Text = "Thanks for the call, I will follow up with notes.",
                SentAt = scheduledFor.AddMinutes(40),
                IngestedAt = scheduledFor.AddMinutes(40),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new MeetingAutoResolutionService(
            factory,
            NullLogger<MeetingAutoResolutionService>.Instance);

        await service.ResolveDueMeetingsAsync(
            scheduledFor.AddMinutes(30),
            CancellationToken.None);

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.Meetings.SingleAsync(CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoMeetingCompletion, entity.ResolutionSource);
    }

    [Fact]
    public void MeetingStatus_OrdinalsStayAlignedAcrossDomainAndContracts()
    {
        Assert.Equal(0, (int)SuperChat.Domain.Features.Intelligence.MeetingStatus.PendingConfirmation);
        Assert.Equal(0, (int)SuperChat.Contracts.Features.WorkItems.MeetingStatus.PendingConfirmation);
        Assert.Equal(1, (int)SuperChat.Domain.Features.Intelligence.MeetingStatus.Confirmed);
        Assert.Equal(1, (int)SuperChat.Contracts.Features.WorkItems.MeetingStatus.Confirmed);
        Assert.Equal(2, (int)SuperChat.Domain.Features.Intelligence.MeetingStatus.Rescheduled);
        Assert.Equal(2, (int)SuperChat.Contracts.Features.WorkItems.MeetingStatus.Rescheduled);
        Assert.Equal(3, (int)SuperChat.Domain.Features.Intelligence.MeetingStatus.Cancelled);
        Assert.Equal(3, (int)SuperChat.Contracts.Features.WorkItems.MeetingStatus.Cancelled);
        Assert.Equal(4, (int)SuperChat.Contracts.Features.WorkItems.MeetingStatus.Completed);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-due-meeting-sweep-{Guid.NewGuid():N}")
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
