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
        var service = new ExtractedItemService(factory);

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

        var service = new ExtractedItemService(factory);
        var items = await service.GetForUserAsync(userId, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("Send contract", items[0].Title);
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
