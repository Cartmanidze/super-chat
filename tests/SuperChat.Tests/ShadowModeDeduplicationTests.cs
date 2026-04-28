using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class ShadowModeDeduplicationTests
{
    [Fact]
    public async Task Second_TryStoreAsync_WithSameExternalMessageId_ReturnsFalse_AndNoDuplicateRow()
    {
        var factory = await CreateFactoryAsync();
        var scheduler = new RecordingScheduler();
        var service = new ChatMessageStore(
            factory,
            scheduler,
            NullLogger<ChatMessageStore>.Instance);

        var userId = Guid.NewGuid();
        var externalChatId = "tg:chat:shadow";
        var externalMessageId = "tg:msg:shadow-1";
        var sentAt = new DateTimeOffset(2026, 04, 19, 12, 0, 0, TimeSpan.Zero);

        var storedMatrix = await service.TryStoreAsync(
            userId, "telegram", externalChatId, externalMessageId,
            "Matrix Path", "hello via matrix", sentAt, CancellationToken.None);

        var storedUserbot = await service.TryStoreAsync(
            userId, "telegram", externalChatId, externalMessageId,
            "Userbot Path", "hello via userbot", sentAt, CancellationToken.None);

        Assert.True(storedMatrix);
        Assert.False(storedUserbot);

        await using var dbContext = await factory.CreateDbContextAsync();
        var rows = await dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.ExternalMessageId == externalMessageId)
            .ToListAsync();

        var single = Assert.Single(rows);
        Assert.Equal("Matrix Path", single.SenderName);
    }

    [Fact]
    public async Task Reverse_UserbotWinsFirst_MatrixDuplicateIsRejected()
    {
        var factory = await CreateFactoryAsync();
        var scheduler = new RecordingScheduler();
        var service = new ChatMessageStore(
            factory,
            scheduler,
            NullLogger<ChatMessageStore>.Instance);

        var userId = Guid.NewGuid();
        var externalChatId = "tg:chat:shadow-2";
        var externalMessageId = "tg:msg:shadow-2";
        var sentAt = new DateTimeOffset(2026, 04, 19, 12, 0, 0, TimeSpan.Zero);

        var storedUserbot = await service.TryStoreAsync(
            userId, "telegram", externalChatId, externalMessageId,
            "Userbot Path", "hello via userbot", sentAt, CancellationToken.None);

        var storedMatrix = await service.TryStoreAsync(
            userId, "telegram", externalChatId, externalMessageId,
            "Matrix Path", "hello via matrix", sentAt, CancellationToken.None);

        Assert.True(storedUserbot);
        Assert.False(storedMatrix);

        await using var dbContext = await factory.CreateDbContextAsync();
        var rows = await dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.ExternalMessageId == externalMessageId)
            .ToListAsync();

        var single = Assert.Single(rows);
        Assert.Equal("Userbot Path", single.SenderName);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync()
    {
        var options = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-shadow-{Guid.NewGuid():N}")
            .Options;

        var factory = new InMemoryDbContextFactory(options);
        await using var dbContext = await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();
        return factory;
    }

    private sealed class InMemoryDbContextFactory(DbContextOptions<SuperChatDbContext> options) : IDbContextFactory<SuperChatDbContext>
    {
        public SuperChatDbContext CreateDbContext() => new(options);

        public Task<SuperChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SuperChatDbContext(options));
    }

    private sealed class RecordingScheduler : IPipelineCommandScheduler
    {
        public bool RequiresTransactionalDispatch => false;

        public Task DispatchChatMessageStoredAsync(
            SuperChatDbContext dbContext,
            Guid userId,
            string source,
            string externalChatId,
            Guid normalizedMessageId,
            string externalMessageId,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
