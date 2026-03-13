using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class ChunkIndexingService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IEmbeddingService embeddingService,
    IQdrantClient qdrantClient,
    IOptions<ChunkIndexingOptions> chunkIndexingOptions,
    TimeProvider timeProvider) : IChunkIndexingService
{
    public async Task<ChunkIndexingRunResult> IndexPendingChunksAsync(CancellationToken cancellationToken)
    {
        var options = chunkIndexingOptions.Value;
        if (!options.Enabled)
        {
            return ChunkIndexingRunResult.Empty;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingChunks = await dbContext.MessageChunks
            .Where(item => item.IndexedAt == null ||
                           item.QdrantPointId == null ||
                           item.QdrantPointId == "" ||
                           item.EmbeddingVersion == null ||
                           item.EmbeddingVersion == "")
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(Math.Max(1, options.BatchSize))
            .ToListAsync(cancellationToken);

        if (pendingChunks.Count == 0)
        {
            return ChunkIndexingRunResult.Empty;
        }

        var now = timeProvider.GetUtcNow();
        var points = new List<QdrantMemoryPoint>(pendingChunks.Count);

        foreach (var chunk in pendingChunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.Text))
            {
                continue;
            }

            var embedding = await embeddingService.EmbedAsync(chunk.Text, cancellationToken);
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
            return new ChunkIndexingRunResult(pendingChunks.Count, 0);
        }

        await qdrantClient.UpsertMemoryPointsAsync(points, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChunkIndexingRunResult(pendingChunks.Count, points.Count);
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

    internal static IReadOnlyDictionary<string, object?> BuildPayload(
        MessageChunkEntity chunk,
        string embeddingVersion)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = chunk.UserId.ToString("D"),
            ["source"] = chunk.Source,
            ["provider"] = chunk.Provider,
            ["transport"] = chunk.Transport,
            ["chat_id"] = chunk.ChatId,
            ["kind"] = chunk.Kind,
            ["ts_from"] = chunk.TsFrom.ToUnixTimeSeconds(),
            ["ts_to"] = chunk.TsTo.ToUnixTimeSeconds(),
            ["chunk_id"] = chunk.Id.ToString("D"),
            ["embedding_version"] = embeddingVersion,
            ["chunk_version"] = chunk.ChunkVersion,
            ["message_count"] = chunk.MessageCount,
            ["content_hash"] = chunk.ContentHash
        };

        if (!string.IsNullOrWhiteSpace(chunk.PeerId))
        {
            payload["peer_id"] = chunk.PeerId;
        }

        if (!string.IsNullOrWhiteSpace(chunk.ThreadId))
        {
            payload["thread_id"] = chunk.ThreadId;
        }

        return payload;
    }
}
