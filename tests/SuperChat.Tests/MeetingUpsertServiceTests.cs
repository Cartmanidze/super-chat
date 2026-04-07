using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class MeetingUpsertServiceTests
{
    [Fact]
    public async Task UpsertRangeAsync_RescheduleFollowUpUpdatesExistingMeetingInRoom()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var originalEventId = "$evt-original";
        var rescheduleEventId = "$evt-reschedule";
        var observedAt = new DateTimeOffset(2026, 04, 06, 11, 37, 23, TimeSpan.Zero);
        var originalScheduledFor = new DateTimeOffset(2026, 04, 06, 15, 00, 00, TimeSpan.Zero);
        var rescheduledFor = new DateTimeOffset(2026, 04, 06, 16, 00, 00, TimeSpan.Zero);
        var joinUrl = "https://telemost.yandex.ru/j/77013557661694";

        var factory = await CreateFactoryAsync(CancellationToken.None);
        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                UserId = userId,
                Title = "Upcoming meeting",
                Summary = "напоминаю про сегодняшнее собеседование в 18:00",
                SourceRoom = roomId,
                SourceEventId = originalEventId,
                ObservedAt = originalScheduledFor.AddHours(-5),
                ScheduledFor = originalScheduledFor,
                Confidence = 0.83,
                CreatedAt = observedAt.AddHours(-1),
                UpdatedAt = observedAt.AddHours(-1)
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = rescheduleEventId,
                SenderName = "glebov84",
                Text = $"переносим на 19:00, ссылка всё та же {joinUrl}",
                SentAt = observedAt,
                IngestedAt = observedAt,
                Processed = false
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new MeetingUpsertService(factory);
        await service.UpsertRangeAsync(
        [
            new ExtractedItem(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                userId,
                ExtractedItemKind.Meeting,
                "Скоро встреча",
                "переносим на 19:00, ссылка всё та же",
                roomId,
                rescheduleEventId,
                null,
                observedAt,
                rescheduledFor,
                new Confidence(0.76))
        ], CancellationToken.None);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meetings = await verificationDbContext.Meetings
            .Where(item => item.UserId == userId && item.SourceRoom == roomId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(CancellationToken.None);

        var meeting = Assert.Single(meetings);
        Assert.Equal(rescheduleEventId, meeting.SourceEventId);
        Assert.Equal("переносим на 19:00, ссылка всё та же", meeting.Summary);
        Assert.Equal(rescheduledFor, meeting.ScheduledFor);
        Assert.Equal(joinUrl, meeting.MeetingJoinUrl);
        Assert.Equal(MeetingStatus.Rescheduled, meeting.Status);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-meeting-upsert-{Guid.NewGuid():N}")
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
