using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

public sealed class ChunkIndexingService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IEmbeddingService embeddingService,
    IQdrantClient qdrantClient,
    IOptions<ChunkIndexingOptions> chunkIndexingOptions,
    TimeProvider timeProvider,
    ILogger<ChunkIndexingService> logger) : IChunkIndexingService
{
    public async Task<ChunkIndexingRunResult> IndexPendingChunksAsync(CancellationToken cancellationToken)
    {
        var options = chunkIndexingOptions.Value;
        if (!options.Enabled)
        {
            return ChunkIndexingRunResult.Empty;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingChunks = await LoadPendingChunksAsync(
            dbContext,
            Math.Max(1, options.BatchSize),
            cancellationToken);

        return await IndexChunksAsync(
            dbContext,
            pendingChunks,
            Math.Max(1, options.BatchSize),
            stopwatch,
            cancellationToken);
    }

    public async Task<ChunkIndexingRunResult> IndexConversationChunksAsync(
        Guid userId,
        string matrixRoomId,
        CancellationToken cancellationToken)
    {
        var options = chunkIndexingOptions.Value;
        if (!options.Enabled)
        {
            return ChunkIndexingRunResult.Empty;
        }

        var batchSize = Math.Max(1, options.BatchSize);
        var totalSelected = 0;
        var totalIndexed = 0;

        while (true)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var pendingChunks = await LoadPendingChunksForConversationAsync(
                dbContext,
                userId,
                matrixRoomId,
                batchSize,
                cancellationToken);

            if (pendingChunks.Count == 0)
            {
                return totalSelected == 0
                    ? ChunkIndexingRunResult.Empty
                    : new ChunkIndexingRunResult(totalSelected, totalIndexed);
            }

            var result = await IndexChunksAsync(
                dbContext,
                pendingChunks,
                batchSize,
                stopwatch,
                cancellationToken);
            totalSelected += result.ChunksSelected;
            totalIndexed += result.ChunksIndexed;

            if (result.ChunksSelected < batchSize)
            {
                return new ChunkIndexingRunResult(totalSelected, totalIndexed);
            }
        }
    }

    private async Task<ChunkIndexingRunResult> IndexChunksAsync(
        SuperChatDbContext dbContext,
        IReadOnlyList<MessageChunkEntity> pendingChunks,
        int batchSize,
        System.Diagnostics.Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        if (pendingChunks.Count == 0)
        {
            return ChunkIndexingRunResult.Empty;
        }

        AiPipelineLog.ChunkIndexingRunStarted(logger, pendingChunks.Count, batchSize);

        try
        {
            var now = timeProvider.GetUtcNow();
            var points = new List<QdrantMemoryPoint>(pendingChunks.Count);

            foreach (var chunk in pendingChunks)
            {
                if (string.IsNullOrWhiteSpace(chunk.Text))
                {
                    continue;
                }

                var embedding = await embeddingService.EmbedAsync(chunk.Text, EmbeddingPurpose.Document, cancellationToken);
                var pointId = BuildPointId(chunk);
                var embeddingVersion = ResolveEmbeddingVersion(embedding);

                points.Add(new QdrantMemoryPoint(
                    pointId,
                    embedding.DenseVector,
                    embedding.SparseVector,
                    BuildPayload(chunk, embeddingVersion)));

                chunk.QdrantPointId = pointId;
                chunk.EmbeddingVersion = embeddingVersion;
                chunk.IndexedAt = now;
                chunk.UpdatedAt = now;
            }

            if (points.Count == 0)
            {
                stopwatch.Stop();
                AiPipelineLog.ChunkIndexingRunCompleted(logger, pendingChunks.Count, 0, stopwatch.ElapsedMilliseconds);
                return new ChunkIndexingRunResult(pendingChunks.Count, 0);
            }

            await qdrantClient.UpsertMemoryPointsAsync(points, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            AiPipelineLog.ChunkIndexingRunCompleted(
                logger,
                pendingChunks.Count,
                points.Count,
                stopwatch.ElapsedMilliseconds);

            return new ChunkIndexingRunResult(pendingChunks.Count, points.Count);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            AiPipelineLog.ChunkIndexingRunFailed(
                logger,
                pendingChunks.Count,
                stopwatch.ElapsedMilliseconds,
                exception);
            throw;
        }
    }

    private static Task<List<MessageChunkEntity>> LoadPendingChunksAsync(
        SuperChatDbContext dbContext,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return dbContext.MessageChunks
            .Where(item => item.IndexedAt == null ||
                           item.QdrantPointId == null ||
                           item.QdrantPointId == "" ||
                           item.EmbeddingVersion == null ||
                           item.EmbeddingVersion == "")
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    private static Task<List<MessageChunkEntity>> LoadPendingChunksForConversationAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string matrixRoomId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return dbContext.MessageChunks
            .Where(item => item.UserId == userId &&
                           item.ChatId == matrixRoomId &&
                           (item.IndexedAt == null ||
                            item.QdrantPointId == null ||
                            item.QdrantPointId == "" ||
                            item.EmbeddingVersion == null ||
                            item.EmbeddingVersion == ""))
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    internal static string BuildPointId(MessageChunkEntity chunk)
    {
        return chunk.Id.ToString("D");
    }

    internal static string ResolveEmbeddingVersion(TextEmbedding embedding)
    {
        if (!string.IsNullOrWhiteSpace(embedding.EmbeddingVersion))
        {
            return embedding.EmbeddingVersion.Trim();
        }

        var provider = embedding.Provider.Trim();
        var model = embedding.Model.Trim();

        if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(model))
        {
            return $"{provider}:{model}";
        }

        return !string.IsNullOrWhiteSpace(model) ? model : provider;
    }

    internal static QdrantChunkPayload BuildPayload(
        MessageChunkEntity chunk,
        string embeddingVersion)
    {
        return new QdrantChunkPayload
        {
            UserId = chunk.UserId.ToString("D"),
            Source = chunk.Source,
            Provider = chunk.Provider,
            Transport = chunk.Transport,
            ChatId = chunk.ChatId,
            PeerId = string.IsNullOrWhiteSpace(chunk.PeerId) ? null : chunk.PeerId,
            ThreadId = string.IsNullOrWhiteSpace(chunk.ThreadId) ? null : chunk.ThreadId,
            Kind = chunk.Kind,
            TsFrom = chunk.TsFrom.ToUnixTimeSeconds(),
            TsTo = chunk.TsTo.ToUnixTimeSeconds(),
            ChunkId = chunk.Id.ToString("D"),
            EmbeddingVersion = embeddingVersion,
            ChunkVersion = chunk.ChunkVersion,
            MessageCount = chunk.MessageCount,
            ContentHash = chunk.ContentHash
        };
    }
}
