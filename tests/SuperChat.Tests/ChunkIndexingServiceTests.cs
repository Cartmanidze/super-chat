using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ChunkIndexingServiceTests
{
    [Fact]
    public async Task IndexPendingChunksAsync_IndexesPendingChunksAndMarksMetadata()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 13, 12, 00, 00, TimeSpan.Zero);
        var factory = await CreateFactoryAsync(CancellationToken.None);

        var pendingChunk = new MessageChunkEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = userId,
            Source = "telegram",
            Provider = "telegram",
            Transport = "matrix_bridge",
            ChatId = "!dm-room:matrix.localhost",
            PeerId = "ivan",
            ThreadId = null,
            Kind = "dialog_chunk",
            Text = "Ivan: Please send the proposal tomorrow.",
            MessageCount = 1,
            TsFrom = now.AddMinutes(-10),
            TsTo = now.AddMinutes(-10),
            ContentHash = "hash-a",
            ChunkVersion = 1,
            CreatedAt = now.AddMinutes(-9),
            UpdatedAt = now.AddMinutes(-9)
        };

        var alreadyIndexedChunk = new MessageChunkEntity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserId = userId,
            Source = "telegram",
            Provider = "telegram",
            Transport = "matrix_bridge",
            ChatId = "!dm-room:matrix.localhost",
            PeerId = "ivan",
            ThreadId = null,
            Kind = "dialog_chunk",
            Text = "You: I will send it today.",
            MessageCount = 1,
            TsFrom = now.AddMinutes(-5),
            TsTo = now.AddMinutes(-5),
            ContentHash = "hash-b",
            ChunkVersion = 1,
            EmbeddingVersion = "bge-m3-v1",
            QdrantPointId = "existing-point",
            IndexedAt = now.AddMinutes(-1),
            CreatedAt = now.AddMinutes(-4),
            UpdatedAt = now.AddMinutes(-1)
        };

        await SeedChunksAsync(factory, [pendingChunk, alreadyIndexedChunk], CancellationToken.None);

        var embeddingService = new RecordingEmbeddingService();
        var qdrantClient = new RecordingQdrantClient();
        var service = CreateService(
            factory,
            embeddingService,
            qdrantClient,
            new ChunkIndexingOptions
            {
                Enabled = true,
                BatchSize = 10
            },
            new FixedTimeProvider(now));

        var result = await service.IndexPendingChunksAsync(CancellationToken.None);

        Assert.Equal(1, result.ChunksSelected);
        Assert.Equal(1, result.ChunksIndexed);
        Assert.Single(embeddingService.RequestedTexts);
        Assert.Single(qdrantClient.UpsertedPoints);

        var point = qdrantClient.UpsertedPoints[0];
        Assert.Equal("11111111-1111-1111-1111-111111111111", point.PointId);
        Assert.Equal("telegram", point.Payload.Provider);
        Assert.Equal("!dm-room:matrix.localhost", point.Payload.ChatId);
        Assert.Equal("ivan", point.Payload.PeerId);
        Assert.Equal(now.AddMinutes(-10).ToUnixTimeSeconds(), point.Payload.TsFrom);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var storedPendingChunk = await dbContext.MessageChunks
            .SingleAsync(item => item.Id == pendingChunk.Id, CancellationToken.None);
        var storedIndexedChunk = await dbContext.MessageChunks
            .SingleAsync(item => item.Id == alreadyIndexedChunk.Id, CancellationToken.None);

        Assert.Equal("11111111-1111-1111-1111-111111111111", storedPendingChunk.QdrantPointId);
        Assert.Equal("bge-m3-v1", storedPendingChunk.EmbeddingVersion);
        Assert.Equal(now, storedPendingChunk.IndexedAt);
        Assert.Equal(now, storedPendingChunk.UpdatedAt);

        Assert.Equal("existing-point", storedIndexedChunk.QdrantPointId);
        Assert.Equal("bge-m3-v1", storedIndexedChunk.EmbeddingVersion);
        Assert.Equal(now.AddMinutes(-1), storedIndexedChunk.IndexedAt);
    }

    [Fact]
    public async Task IndexPendingChunksAsync_RespectsBatchSize()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 03, 13, 12, 00, 00, TimeSpan.Zero);
        var factory = await CreateFactoryAsync(CancellationToken.None);

        var chunks = Enumerable.Range(1, 3)
            .Select(index => new MessageChunkEntity
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-00000000000{index}"),
                UserId = userId,
                Source = "telegram",
                Provider = "telegram",
                Transport = "matrix_bridge",
                ChatId = "!room:matrix.localhost",
                Kind = "dialog_chunk",
                Text = $"Alice: message {index}",
                MessageCount = 1,
                TsFrom = now.AddMinutes(-index),
                TsTo = now.AddMinutes(-index),
                ContentHash = $"hash-{index}",
                ChunkVersion = 1,
                CreatedAt = now.AddMinutes(-index),
                UpdatedAt = now.AddMinutes(-index)
            })
            .ToArray();

        await SeedChunksAsync(factory, chunks, CancellationToken.None);

        var service = CreateService(
            factory,
            new RecordingEmbeddingService(),
            new RecordingQdrantClient(),
            new ChunkIndexingOptions
            {
                Enabled = true,
                BatchSize = 2
            },
            new FixedTimeProvider(now));

        var result = await service.IndexPendingChunksAsync(CancellationToken.None);

        Assert.Equal(2, result.ChunksSelected);
        Assert.Equal(2, result.ChunksIndexed);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var indexedCount = await dbContext.MessageChunks.CountAsync(item => item.IndexedAt != null, CancellationToken.None);
        Assert.Equal(2, indexedCount);
    }

    [Fact]
    public void ResolveEmbeddingVersion_FallsBackToProviderAndModel()
    {
        var embedding = new TextEmbedding(
            [0.1f, 0.2f],
            new SparseTextVector([1], [0.5f]),
            "mock",
            "BAAI/bge-m3",
            string.Empty);

        var result = ChunkIndexingService.ResolveEmbeddingVersion(embedding);

        Assert.Equal("mock:BAAI/bge-m3", result);
    }

    private static ChunkIndexingService CreateService(
        IDbContextFactory<SuperChatDbContext> factory,
        IEmbeddingService embeddingService,
        IQdrantClient qdrantClient,
        ChunkIndexingOptions options,
        TimeProvider timeProvider)
    {
        return new ChunkIndexingService(
            factory,
            embeddingService,
            qdrantClient,
            Options.Create(options),
            timeProvider,
            NullLogger<ChunkIndexingService>.Instance);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-chunk-indexing-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static async Task SeedChunksAsync(
        IDbContextFactory<SuperChatDbContext> factory,
        IEnumerable<MessageChunkEntity> chunks,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        dbContext.MessageChunks.AddRange(chunks);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public List<string> RequestedTexts { get; } = [];

        public Task<TextEmbedding> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken)
        {
            RequestedTexts.Add(text);

            return Task.FromResult(new TextEmbedding(
                [0.1f, 0.2f, 0.3f],
                new SparseTextVector([7, 11], [0.6f, 0.4f]),
                "mock",
                "BAAI/bge-m3",
                "bge-m3-v1"));
        }
    }

    private sealed class RecordingQdrantClient : IQdrantClient
    {
        public List<QdrantMemoryPoint> UpsertedPoints { get; } = [];

        public Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpsertMemoryPointsAsync(IReadOnlyList<QdrantMemoryPoint> points, CancellationToken cancellationToken)
        {
            UpsertedPoints.AddRange(points);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QdrantQueryPoint>> QueryMemoryPointsAsync(QdrantHybridQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<QdrantQueryPoint>>([]);
        }
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
