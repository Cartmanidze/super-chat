using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class MeetingProjectionServiceTests
{
    [Fact]
    public void ShouldProcessUser_ReturnsTrueAtCheckpointBoundary()
    {
        var checkpoint = new MeetingProjectionCheckpointEntity
        {
            UserId = Guid.NewGuid(),
            LastObservedChunkUpdatedAt = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero)
        };

        var result = MeetingProjectionService.ShouldProcessUser(
            checkpoint.LastObservedChunkUpdatedAt.Value,
            checkpoint);

        Assert.True(result);
    }

    [Fact]
    public void FilterNewChunks_UsesGuidTieBreakerForSameUpdatedAt()
    {
        var updatedAt = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var earlierId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var laterId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var chunks = new List<MessageChunkEntity>
        {
            CreateChunkEntity(earlierId, Guid.NewGuid(), "!room:matrix.localhost", updatedAt, updatedAt, "hash-earlier", "Alice: ping"),
            CreateChunkEntity(laterId, Guid.NewGuid(), "!room:matrix.localhost", updatedAt, updatedAt, "hash-later", "Alice: pong")
        };

        var checkpoint = new MeetingProjectionCheckpointEntity
        {
            UserId = Guid.NewGuid(),
            LastObservedChunkUpdatedAt = updatedAt,
            LastObservedChunkId = earlierId
        };

        var result = MeetingProjectionService.FilterNewChunks(chunks, checkpoint);

        Assert.Single(result);
        Assert.Equal(laterId, result[0].Id);
    }

    [Fact]
    public async Task ProjectPendingChunkMeetingsAsync_UpsertsChunkMeetingsAndKeepsMessageMeetings()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var now = new DateTimeOffset(2026, 03, 13, 09, 30, 00, TimeSpan.Zero);
        var factory = await CreateFactoryAsync(CancellationToken.None);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.MessageChunks.Add(CreateChunkEntity(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                userId,
                roomId,
                now.AddMinutes(-10),
                now.AddMinutes(-5),
                "chunk-hash-meeting",
                """
                Alex: давай зафиксируем
                You: итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это
                Alex: ок
                """));

            dbContext.Meetings.AddRange(
            [
                new MeetingEntity
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    UserId = userId,
                    Title = "Upcoming meeting",
                    Summary = "old chunk projection",
                    SourceRoom = roomId,
                    SourceEventId = "chunk:stale-hash",
                    ObservedAt = now.AddMinutes(-20),
                    ScheduledFor = now.AddHours(1),
                    Confidence = 0.50,
                    CreatedAt = now.AddMinutes(-20),
                    UpdatedAt = now.AddMinutes(-20)
                },
                new MeetingEntity
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    UserId = userId,
                    Title = "Upcoming meeting",
                    Summary = "Message-level meeting",
                    SourceRoom = roomId,
                    SourceEventId = "$evt-message",
                    ObservedAt = now.AddMinutes(-15),
                    ScheduledFor = now.AddHours(2),
                    Confidence = 0.84,
                    CreatedAt = now.AddMinutes(-15),
                    UpdatedAt = now.AddMinutes(-15)
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory, now);
        var result = await service.ProjectPendingChunkMeetingsAsync(CancellationToken.None);

        Assert.Equal(1, result.UsersProcessed);
        Assert.Equal(1, result.RoomsRebuilt);
        Assert.Equal(1, result.MeetingsProjected);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var meetings = await verificationDbContext.Meetings
            .OrderBy(item => item.SourceEventId)
            .ToListAsync(CancellationToken.None);
        var checkpoint = await verificationDbContext.MeetingProjectionCheckpoints
            .SingleAsync(item => item.UserId == userId, CancellationToken.None);

        Assert.Equal(2, meetings.Count);
        Assert.DoesNotContain(meetings, item => item.SourceEventId == "chunk:stale-hash");
        Assert.Contains(meetings, item => item.SourceEventId == "$evt-message");

        var chunkMeeting = Assert.Single(meetings, item => item.SourceEventId.StartsWith("chunk:", StringComparison.Ordinal));
        Assert.Equal("итого, у нас будет встреча в 20:00 по мск времени сегодня, подтверждаю это", chunkMeeting.Summary);
        Assert.Equal(new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero), chunkMeeting.ScheduledFor);
        Assert.Equal(now.AddMinutes(-5), checkpoint.LastObservedChunkUpdatedAt);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), checkpoint.LastObservedChunkId);
    }

    [Fact]
    public async Task ProjectPendingChunkMeetingsAsync_RemovesStaleChunkMeetingsWhenSignalDisappears()
    {
        var userId = Guid.NewGuid();
        var roomId = "!dm:matrix.localhost";
        var now = new DateTimeOffset(2026, 03, 13, 09, 30, 00, TimeSpan.Zero);
        var factory = await CreateFactoryAsync(CancellationToken.None);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.MessageChunks.Add(CreateChunkEntity(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                userId,
                roomId,
                now.AddMinutes(-10),
                now,
                "chunk-hash-neutral",
                "You: просто заметка без времени"));

            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                UserId = userId,
                Title = "Upcoming meeting",
                Summary = "old chunk projection",
                SourceRoom = roomId,
                SourceEventId = "chunk:stale-hash",
                ObservedAt = now.AddMinutes(-20),
                ScheduledFor = now.AddHours(1),
                Confidence = 0.50,
                CreatedAt = now.AddMinutes(-20),
                UpdatedAt = now.AddMinutes(-20)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory, now);
        var result = await service.ProjectPendingChunkMeetingsAsync(CancellationToken.None);

        Assert.Equal(0, result.MeetingsProjected);

        await using var verificationDbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var remainingChunkMeetings = await verificationDbContext.Meetings
            .Where(item => item.UserId == userId && item.SourceRoom == roomId)
            .ToListAsync(CancellationToken.None);

        Assert.Empty(remainingChunkMeetings);
    }

    private static MeetingProjectionService CreateService(
        IDbContextFactory<SuperChatDbContext> factory,
        DateTimeOffset now)
    {
        return new MeetingProjectionService(
            factory,
            Options.Create(new MeetingProjectionOptions
            {
                Enabled = true
            }),
            CreatePilotOptions(),
            new FixedTimeProvider(now));
    }

    private static PilotOptions CreatePilotOptions()
    {
        return new PilotOptions
        {
            TodayTimeZoneId = "Europe/Moscow"
        };
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-meeting-projection-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static MessageChunkEntity CreateChunkEntity(
        Guid id,
        Guid userId,
        string roomId,
        DateTimeOffset tsFrom,
        DateTimeOffset updatedAt,
        string contentHash,
        string text)
    {
        return new MessageChunkEntity
        {
            Id = id,
            UserId = userId,
            Source = "telegram",
            Provider = "telegram",
            Transport = "matrix_bridge",
            ChatId = roomId,
            Kind = "dialog_chunk",
            Text = text,
            MessageCount = Math.Max(1, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length),
            TsFrom = tsFrom,
            TsTo = updatedAt,
            ContentHash = contentHash,
            ChunkVersion = 1,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
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
