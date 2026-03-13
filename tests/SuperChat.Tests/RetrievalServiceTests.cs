using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class RetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_ReturnsRankedChunksAndPersistsRetrievalLog()
    {
        var userId = Guid.NewGuid();
        var chunkId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var factory = await CreateFactoryAsync(CancellationToken.None);

        await SeedChunkAsync(factory, new MessageChunkEntity
        {
            Id = chunkId,
            UserId = userId,
            Source = "telegram",
            Provider = "telegram",
            Transport = "matrix_bridge",
            ChatId = "!ivan-room:matrix.localhost",
            PeerId = "ivan",
            Kind = "dialog_chunk",
            Text = "Ivan: Please send the proposal tomorrow.\nYou: I will send it today.",
            MessageCount = 2,
            TsFrom = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero),
            TsTo = new DateTimeOffset(2026, 03, 13, 10, 05, 00, TimeSpan.Zero),
            ContentHash = "hash-a",
            ChunkVersion = 1,
            EmbeddingVersion = "bge-m3-v1",
            QdrantPointId = chunkId.ToString("D"),
            IndexedAt = new DateTimeOffset(2026, 03, 13, 10, 06, 00, TimeSpan.Zero),
            CreatedAt = new DateTimeOffset(2026, 03, 13, 10, 06, 00, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 03, 13, 10, 06, 00, TimeSpan.Zero)
        }, CancellationToken.None);

        var qdrantClient = new RecordingQdrantClient(
        [
            new QdrantQueryPoint(
                chunkId.ToString("D"),
                0.93,
                new Dictionary<string, object?>
                {
                    ["chunk_id"] = chunkId.ToString("D"),
                    ["chat_id"] = "!ivan-room:matrix.localhost",
                    ["kind"] = "dialog_chunk"
                })
        ]);

        var embeddingService = new RecordingEmbeddingService();
        var service = new RetrievalService(
            factory,
            embeddingService,
            qdrantClient,
            Options.Create(new RetrievalOptions
            {
                Enabled = true,
                PrefetchLimit = 24,
                ResultLimit = 8
            }),
            Options.Create(new QdrantOptions
            {
                MemoryCollectionName = "memory_bgem3_v1"
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 03, 13, 12, 00, 00, TimeSpan.Zero)),
            NullLogger<RetrievalService>.Instance);

        var results = await service.RetrieveAsync(
            new RetrievalRequest(userId, "Что я обещал Ивану?", "chat_custom", PeerId: "ivan"),
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(chunkId, result.ChunkId);
        Assert.Equal("!ivan-room:matrix.localhost", result.ChatId);
        Assert.Equal(0.93, result.Score, 3);

        var issuedQuery = Assert.Single(qdrantClient.Requests);
        Assert.Equal(userId.ToString("D"), issuedQuery.UserId);
        Assert.Equal("ivan", issuedQuery.PeerId);
        Assert.Equal(24, issuedQuery.PrefetchLimit);

        await using var dbContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var retrievalLog = await dbContext.RetrievalLogs.SingleAsync(CancellationToken.None);
        Assert.Equal(userId, retrievalLog.UserId);
        Assert.Equal("chat_custom", retrievalLog.QueryKind);
        Assert.Equal(1, retrievalLog.CandidateCount);
        Assert.Contains(chunkId.ToString("D"), retrievalLog.SelectedChunkIdsJson ?? string.Empty, StringComparison.Ordinal);

        using var filtersDoc = JsonDocument.Parse(retrievalLog.FiltersJson!);
        Assert.Equal(userId.ToString("D"), filtersDoc.RootElement.GetProperty("user_id").GetString());
        Assert.Equal("ivan", filtersDoc.RootElement.GetProperty("peer_id").GetString());
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsEmpty_WhenDisabled()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var qdrantClient = new RecordingQdrantClient([]);
        var service = new RetrievalService(
            factory,
            new RecordingEmbeddingService(),
            qdrantClient,
            Options.Create(new RetrievalOptions
            {
                Enabled = false
            }),
            Options.Create(new QdrantOptions
            {
                MemoryCollectionName = "memory_bgem3_v1"
            }),
            TimeProvider.System,
            NullLogger<RetrievalService>.Instance);

        var results = await service.RetrieveAsync(
            new RetrievalRequest(Guid.NewGuid(), "test", "chat_custom"),
            CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(qdrantClient.Requests);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-retrieval-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private static async Task SeedChunkAsync(
        IDbContextFactory<SuperChatDbContext> factory,
        MessageChunkEntity chunk,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        dbContext.MessageChunks.Add(chunk);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public Task<TextEmbedding> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TextEmbedding(
                [0.1f, 0.2f, 0.3f],
                new SparseTextVector([7, 11], [0.6f, 0.4f]),
                "mock",
                "BAAI/bge-m3",
                "bge-m3-v1"));
        }
    }

    private sealed class RecordingQdrantClient(IReadOnlyList<QdrantQueryPoint> response) : IQdrantClient
    {
        public List<QdrantHybridQuery> Requests { get; } = [];

        public Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpsertMemoryPointsAsync(IReadOnlyList<QdrantMemoryPoint> points, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QdrantQueryPoint>> QueryMemoryPointsAsync(QdrantHybridQuery request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response);
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
