using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class MeetingUpsertServiceTests
{
    [Fact]
    public async Task UpsertRangeAsync_RescheduleWithoutFinalTime_StoresMeetingWithoutScheduledFor()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var originalEventId = "$evt-original";
        var rescheduleEventId = "$evt-reschedule";
        var observedAt = new DateTimeOffset(2026, 04, 09, 08, 46, 02, TimeSpan.Zero);

        var factory = await CreateFactoryAsync(CancellationToken.None);
        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = Guid.Parse("10101010-1010-1010-1010-101010101010"),
                UserId = userId,
                Title = "Upcoming meeting",
                Summary = "Приглашение на интервью в пятницу, 10 апреля, с 15:30 до 16:30 по Мск.",
                SourceRoom = roomId,
                SourceEventId = originalEventId,
                Person = "Глеб",
                ObservedAt = observedAt.AddDays(-1),
                ScheduledFor = new DateTimeOffset(2026, 04, 10, 12, 30, 00, TimeSpan.Zero),
                Confidence = 0.83,
                Status = MeetingStatus.Confirmed,
                CreatedAt = observedAt.AddDays(-1),
                UpdatedAt = observedAt.AddDays(-1)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance);
        await service.UpsertRangeAsync(
        [
            new ExtractedItem(
                Guid.Parse("20202020-2020-2020-2020-202020202020"),
                userId,
                ExtractedItemKind.Meeting,
                "Перенос интервью",
                "Коллеги попросили перенести интервью, предложены варианты на 13, 16, 17 апреля.",
                roomId,
                rescheduleEventId,
                "Глеб",
                observedAt,
                null,
                new Confidence(0.80))
        ], CancellationToken.None);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meeting = await verificationDbContext.Meetings.SingleAsync(item => item.UserId == userId, CancellationToken.None);

        Assert.Equal(rescheduleEventId, meeting.SourceEventId);
        Assert.Equal(MeetingStatus.Rescheduled, meeting.Status);
        Assert.Null(meeting.ScheduledFor);
        Assert.Equal("Коллеги попросили перенести интервью, предложены варианты на 13, 16, 17 апреля.", meeting.Summary);
    }

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
                ExternalChatId = roomId,
                ExternalMessageId = rescheduleEventId,
                SenderName = "glebov84",
                Text = $"переносим на 19:00, ссылка всё та же {joinUrl}",
                SentAt = observedAt,
                ReceivedAt = observedAt,
                Processed = false
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance);
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

    [Fact]
    public async Task UpsertRangeAsync_DuplicateContentWithDifferentSourceEventId_CreatesOneMeeting()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var observedAt = new DateTimeOffset(2026, 04, 07, 08, 00, 00, TimeSpan.Zero);
        var scheduledFor = new DateTimeOffset(2026, 04, 07, 15, 00, 00, TimeSpan.Zero);

        var factory = await CreateFactoryAsync(CancellationToken.None);
        var service = new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance);

        await service.UpsertRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Upcoming meeting",
                "встреча в 18:00 по мск",
                roomId,
                "$evt-first",
                null,
                observedAt,
                scheduledFor,
                new Confidence(0.85))
        ], CancellationToken.None);

        await service.UpsertRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Upcoming meeting",
                "встреча в 18:00 по мск",
                roomId,
                "$evt-second",
                null,
                observedAt.AddMinutes(5),
                scheduledFor,
                new Confidence(0.90))
        ], CancellationToken.None);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meetings = await verificationDbContext.Meetings
            .Where(item => item.UserId == userId && item.SourceRoom == roomId)
            .ToListAsync(CancellationToken.None);

        Assert.Single(meetings);
    }

    [Fact]
    public async Task UpsertRangeAsync_DedupKeyMatchesProjectionPath_WhenDueAtHasNonUtcOffset()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var existingId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var existingEventId = "$evt-existing";
        var incomingEventId = "$evt-offset";
        var observedAt = new DateTimeOffset(2026, 04, 07, 08, 15, 00, TimeSpan.Zero);
        var scheduledForUtc = new DateTimeOffset(2026, 04, 07, 15, 00, 00, TimeSpan.Zero);
        var scheduledForPlusThree = new DateTimeOffset(2026, 04, 07, 18, 00, 00, TimeSpan.FromHours(3));
        const string summary = "встреча в 18:00 по мск";

        var factory = await CreateFactoryAsync(CancellationToken.None);
        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = existingId,
                UserId = userId,
                Title = "Existing meeting",
                Summary = summary,
                SourceRoom = roomId,
                SourceEventId = existingEventId,
                ObservedAt = observedAt.AddMinutes(-20),
                ScheduledFor = scheduledForUtc,
                Confidence = 0.70,
                CreatedAt = observedAt.AddHours(-1),
                UpdatedAt = observedAt.AddHours(-1)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance);
        await service.UpsertRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Offset meeting",
                summary,
                roomId,
                incomingEventId,
                null,
                observedAt,
                scheduledForPlusThree,
                new Confidence(0.93))
        ], CancellationToken.None);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meetings = await verificationDbContext.Meetings
            .Where(item => item.UserId == userId && item.SourceRoom == roomId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(CancellationToken.None);

        var meeting = Assert.Single(meetings);
        Assert.Equal(existingId, meeting.Id);
        Assert.Equal(existingEventId, meeting.SourceEventId);
        Assert.Equal(scheduledForUtc, meeting.ScheduledFor);
        Assert.Equal(0.93, meeting.Confidence, 3);
    }

    [Fact]
    public async Task UpsertRangeAsync_PrefersExtractionEntityOverChunkEntity_ForSameDedupKey()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var scheduledFor = new DateTimeOffset(2026, 04, 07, 15, 00, 00, TimeSpan.Zero);
        var observedAt = new DateTimeOffset(2026, 04, 07, 08, 00, 00, TimeSpan.Zero);
        const string summary = "встреча в 18:00 по мск";
        var chunkId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var extractionId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        const string chunkSourceEventId = "chunk:old-hash";
        const string extractionSourceEventId = "$evt-extraction";

        var factory = await CreateFactoryAsync(CancellationToken.None);
        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = chunkId,
                UserId = userId,
                Title = "Chunk title",
                Summary = summary,
                SourceRoom = roomId,
                SourceEventId = chunkSourceEventId,
                Person = "Chunk person",
                ObservedAt = observedAt.AddMinutes(-15),
                ScheduledFor = scheduledFor,
                Confidence = 0.61,
                CreatedAt = observedAt.AddMinutes(-15),
                UpdatedAt = observedAt.AddMinutes(-15)
            });
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = extractionId,
                UserId = userId,
                Title = "Extraction title",
                Summary = summary,
                SourceRoom = roomId,
                SourceEventId = extractionSourceEventId,
                Person = "Extraction person",
                ObservedAt = observedAt.AddMinutes(-10),
                ScheduledFor = scheduledFor,
                Confidence = 0.74,
                CreatedAt = observedAt.AddMinutes(-10),
                UpdatedAt = observedAt.AddMinutes(-10)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = new MeetingUpsertService(factory, NullLogger<MeetingUpsertService>.Instance);
        await service.UpsertRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Updated title",
                summary,
                roomId,
                "$evt-new",
                "Updated person",
                observedAt,
                scheduledFor,
                new Confidence(0.95))
        ], CancellationToken.None);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meetings = await verificationDbContext.Meetings
            .Where(item => item.UserId == userId && item.SourceRoom == roomId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(2, meetings.Count);

        var chunkMeeting = Assert.Single(meetings, item => item.Id == chunkId);
        var extractionMeeting = Assert.Single(meetings, item => item.Id == extractionId);

        Assert.Equal("Chunk title", chunkMeeting.Title);
        Assert.Equal("Chunk person", chunkMeeting.Person);
        Assert.Equal("Updated title", extractionMeeting.Title);
        Assert.Equal("Updated person", extractionMeeting.Person);
        Assert.Equal(0.95, extractionMeeting.Confidence, 3);
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
