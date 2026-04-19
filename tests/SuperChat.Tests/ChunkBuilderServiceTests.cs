using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class ChunkBuilderServiceTests
{
    [Fact]
    public void ShouldProcessUser_ReturnsTrueAtCheckpointBoundary()
    {
        var checkpoint = new ChunkBuildCheckpointEntity
        {
            UserId = Guid.NewGuid(),
            LastObservedReceivedAt = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero)
        };

        var result = ChunkBuilderService.ShouldProcessUser(
            checkpoint.LastObservedReceivedAt.Value,
            checkpoint);

        Assert.True(result);
    }

    [Fact]
    public void FilterNewMessages_UsesGuidTieBreakerForSameReceivedAt()
    {
        var receivedAt = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var earlierId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var laterId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var messages = new List<NormalizedMessageEntity>
        {
            CreateEntity(earlierId, receivedAt),
            CreateEntity(laterId, receivedAt)
        };

        var checkpoint = new ChunkBuildCheckpointEntity
        {
            UserId = Guid.NewGuid(),
            LastObservedReceivedAt = receivedAt,
            LastObservedMessageId = earlierId
        };

        var result = ChunkBuilderService.FilterNewMessages(messages, checkpoint);

        Assert.Single(result);
        Assert.Equal(laterId, result[0].Id);
    }

    [Fact]
    public void BuildChunkEntities_SplitsOnConfiguredTimeGap()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var baseTime = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var options = new ChunkingOptions
        {
            MaxGapMinutes = 10,
            MaxMessagesPerChunk = 8,
            MaxChunkCharacters = 1600
        };

        var messages = new List<NormalizedMessage>
        {
            CreateDomainMessage(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), userId, roomId, "Alice", "Первое", baseTime, baseTime),
            CreateDomainMessage(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), userId, roomId, "You", "Второе", baseTime.AddMinutes(5), baseTime.AddMinutes(5)),
            CreateDomainMessage(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), userId, roomId, "Alice", "Третье", baseTime.AddMinutes(30), baseTime.AddMinutes(30))
        };

        var chunks = ChunkBuilderService.BuildChunkEntities(userId, roomId, messages, options, baseTime.AddHours(1));

        Assert.Equal(2, chunks.Count);
        Assert.Equal(2, chunks[0].MessageCount);
        Assert.Equal("Alice: Первое\nYou: Второе", chunks[0].Text);
        Assert.Equal(1, chunks[1].MessageCount);
        Assert.Equal("Alice: Третье", chunks[1].Text);
    }

    [Fact]
    public async Task BuildPendingChunksAsync_RebuildsTailAndLeavesExtractionFlagsUntouched()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var baseTime = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var options = new ChunkingOptions
        {
            Enabled = true,
            MaxGapMinutes = 15,
            MaxMessagesPerChunk = 8,
            MaxChunkCharacters = 1600
        };

        var factory = await CreateFactoryAsync(CancellationToken.None);

        await SeedMessagesAsync(factory, userId, roomId, baseTime, count: 2, startingIndex: 1, CancellationToken.None);

        var service = CreateService(factory, options);
        var firstRun = await service.BuildPendingChunksAsync(CancellationToken.None);

        Assert.Equal(1, firstRun.UsersProcessed);
        Assert.Equal(1, firstRun.RoomsRebuilt);
        Assert.Equal(1, firstRun.ChunksWritten);

        await SeedMessagesAsync(factory, userId, roomId, baseTime.AddMinutes(8), count: 1, startingIndex: 3, CancellationToken.None);

        var secondRun = await service.BuildPendingChunksAsync(CancellationToken.None);

        Assert.Equal(1, secondRun.UsersProcessed);
        Assert.Equal(1, secondRun.RoomsRebuilt);
        Assert.Equal(1, secondRun.ChunksWritten);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var chunks = await dbContext.MessageChunks
            .OrderBy(item => item.TsFrom)
            .ToListAsync(CancellationToken.None);

        var checkpoint = await dbContext.ChunkBuildCheckpoints
            .SingleAsync(item => item.UserId == userId, CancellationToken.None);

        var normalizedMessages = await dbContext.NormalizedMessages
            .OrderBy(item => item.SentAt)
            .ToListAsync(CancellationToken.None);

        Assert.Single(chunks);
        Assert.Equal(3, chunks[0].MessageCount);
        Assert.Contains("Message 3", chunks[0].Text, StringComparison.Ordinal);
        Assert.Equal(normalizedMessages[^1].ReceivedAt, checkpoint.LastObservedReceivedAt);
        Assert.Equal(normalizedMessages[^1].Id, checkpoint.LastObservedMessageId);
        Assert.All(normalizedMessages, item => Assert.False(item.Processed));
    }

    [Fact]
    public async Task BuildConversationChunksAsync_RebuildsOnlyRequestedRoom()
    {
        var userId = Guid.NewGuid();
        var targetRoomId = "!target:matrix.localhost";
        var otherRoomId = "!other:matrix.localhost";
        var baseTime = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var options = new ChunkingOptions
        {
            Enabled = true,
            MaxGapMinutes = 15,
            MaxMessagesPerChunk = 8,
            MaxChunkCharacters = 1600
        };

        var factory = await CreateFactoryAsync(CancellationToken.None);
        await SeedMessagesAsync(factory, userId, targetRoomId, baseTime, count: 2, startingIndex: 1, CancellationToken.None);
        await SeedMessagesAsync(factory, userId, otherRoomId, baseTime, count: 2, startingIndex: 101, CancellationToken.None);

        var service = CreateService(factory, options);
        var result = await service.BuildConversationChunksAsync(
            userId,
            targetRoomId,
            baseTime.AddMinutes(-options.MaxGapMinutes),
            CancellationToken.None);

        Assert.Equal(1, result.UsersProcessed);
        Assert.Equal(1, result.RoomsRebuilt);
        Assert.Equal(1, result.ChunksWritten);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var chunks = await dbContext.MessageChunks
            .OrderBy(item => item.ChatId)
            .ToListAsync(CancellationToken.None);

        Assert.Single(chunks);
        Assert.Equal(targetRoomId, chunks[0].ChatId);
    }

    private static ChunkBuilderService CreateService(
        IDbContextFactory<SuperChatDbContext> factory,
        ChunkingOptions options)
    {
        return new ChunkBuilderService(
            factory,
            Options.Create(options),
            TimeProvider.System);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-chunking-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static async Task SeedMessagesAsync(
        IDbContextFactory<SuperChatDbContext> factory,
        Guid userId,
        string roomId,
        DateTimeOffset startTime,
        int count,
        int startingIndex,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            var messageIndex = startingIndex + index;
            var sentAt = startTime.AddMinutes(index);
            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                ExternalChatId = roomId,
                ExternalMessageId = $"$evt-{messageIndex}",
                SenderName = messageIndex % 2 == 0 ? "You" : "Alice",
                Text = $"Message {messageIndex}",
                SentAt = sentAt,
                ReceivedAt = sentAt.AddSeconds(5),
                Processed = false
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static NormalizedMessageEntity CreateEntity(Guid id, DateTimeOffset receivedAt)
    {
        return new NormalizedMessageEntity
        {
            Id = id,
            UserId = Guid.NewGuid(),
            Source = "telegram",
            ExternalChatId = "!room:matrix.localhost",
            ExternalMessageId = "$evt",
            SenderName = "Alice",
            Text = "Hello",
            SentAt = receivedAt,
            ReceivedAt = receivedAt,
            Processed = false
        };
    }

    private static NormalizedMessage CreateDomainMessage(
        Guid id,
        Guid userId,
        string roomId,
        string sender,
        string text,
        DateTimeOffset sentAt,
        DateTimeOffset receivedAt)
    {
        return new NormalizedMessage(
            id,
            userId,
            "telegram",
            roomId,
            $"$evt-{id:N}",
            sender,
            text,
            sentAt,
            receivedAt,
            false);
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
