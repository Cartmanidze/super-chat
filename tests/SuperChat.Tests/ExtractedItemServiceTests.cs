using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ExtractedItemServiceTests
{
    [Fact]
    public async Task AddRangeAsync_DoesNotPersistGenericFollowUpCandidate()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);

        await service.AddRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Task,
                "Follow-up candidate",
                "video.mp4",
                "!room:matrix.localhost",
                "$evt-1",
                null,
                DateTimeOffset.UtcNow,
                null,
                0.51)
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var count = await dbContext.ExtractedItems.CountAsync(CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetForUserAsync_FiltersLegacyFollowUpCandidateItems()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.ExtractedItems.AddRange(
            [
                new ExtractedItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Follow-up candidate",
                    Summary = "Обманул",
                    SourceRoom = "!room:matrix.localhost",
                    SourceEventId = "$evt-legacy",
                    ObservedAt = DateTimeOffset.UtcNow,
                    Confidence = 0.51
                },
                new ExtractedItemEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = ExtractedItemKind.Task,
                    Title = "Send contract",
                    Summary = "Please send the contract tomorrow.",
                    SourceRoom = "!sales:matrix.localhost",
                    SourceEventId = "$evt-real",
                    ObservedAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    Confidence = 0.91
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var service = CreateService(factory);
        var items = await service.GetForUserAsync(userId, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("Send contract", items[0].Title);
    }

    [Fact]
    public async Task AddRangeAsync_ProjectsMeetingsIntoSeparateTable()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var service = CreateService(factory);
        var dueAt = new DateTimeOffset(2026, 03, 13, 11, 00, 00, TimeSpan.FromHours(6));

        await service.AddRangeAsync(
        [
            new ExtractedItem(
                Guid.NewGuid(),
                userId,
                ExtractedItemKind.Meeting,
                "Upcoming meeting",
                "Мб заехать за тобой в 11?",
                "!friends:matrix.localhost",
                "$evt-meeting",
                null,
                dueAt.AddHours(-1),
                dueAt,
                0.86)
        ],
        CancellationToken.None);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var projectedMeeting = await dbContext.Meetings.SingleAsync(CancellationToken.None);

        Assert.Equal(userId, projectedMeeting.UserId);
        Assert.Equal("$evt-meeting", projectedMeeting.SourceEventId);
        Assert.Equal(dueAt.ToUniversalTime(), projectedMeeting.ScheduledFor);
        Assert.Equal(TimeSpan.Zero, projectedMeeting.ScheduledFor.Offset);
        Assert.Equal("Мб заехать за тобой в 11?", projectedMeeting.Summary);

        var meetings = await new MeetingService(factory).GetUpcomingAsync(userId, dueAt.AddHours(-2), 10, CancellationToken.None);
        Assert.Single(meetings);
        Assert.Equal(dueAt.ToUniversalTime(), meetings[0].ScheduledFor);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-extracted-items-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static ExtractedItemService CreateService(IDbContextFactory<SuperChatDbContext> factory)
    {
        return new ExtractedItemService(factory, new MeetingService(factory));
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
