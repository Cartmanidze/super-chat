using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class RetrievalService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IEmbeddingService embeddingService,
    IQdrantClient qdrantClient,
    IOptions<RetrievalOptions> retrievalOptions,
    IOptions<QdrantOptions> qdrantOptions,
    TimeProvider timeProvider,
    ILogger<RetrievalService> logger) : IRetrievalService
{
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken)
    {
        var normalizedQuery = request.QueryText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var options = retrievalOptions.Value;
        if (!options.Enabled)
        {
            return [];
        }

        var stopwatch = Stopwatch.StartNew();
        var resultLimit = Math.Max(1, request.Limit ?? options.ResultLimit);
        var prefetchLimit = Math.Max(1, options.PrefetchLimit);

        AiPipelineLog.RetrievalStarted(
            logger,
            request.QueryKind,
            normalizedQuery.Length,
            resultLimit,
            prefetchLimit);

        try
        {
            var embedding = await embeddingService.EmbedAsync(normalizedQuery, EmbeddingPurpose.Query, cancellationToken);
            var query = new QdrantHybridQuery(
                embedding.DenseVector,
                embedding.SparseVector,
                request.UserId.ToString("D"),
                request.ChatId,
                request.PeerId,
                request.Kind,
                prefetchLimit,
                resultLimit);

            var matches = await qdrantClient.QueryMemoryPointsAsync(query, cancellationToken);
            var chunkIds = matches
                .Select(TryExtractChunkId)
                .Where(static item => item.HasValue)
                .Select(static item => item!.Value)
                .Distinct()
                .ToList();

            await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                var chunksById = await dbContext.MessageChunks
                    .AsNoTracking()
                    .Where(item => chunkIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, cancellationToken);

                var retrievedChunks = matches
                    .Select(match => new
                    {
                        ChunkId = TryExtractChunkId(match),
                        Match = match
                    })
                    .Where(item => item.ChunkId.HasValue)
                    .Select(item => new
                    {
                        Chunk = chunksById.GetValueOrDefault(item.ChunkId!.Value),
                        item.Match
                    })
                    .Where(item => item.Chunk is not null)
                    .Select(item => new RetrievedChunk(
                        item.Chunk!.Id,
                        item.Chunk.ChatId,
                        item.Chunk.PeerId,
                        item.Chunk.Kind,
                        item.Chunk.Text,
                        item.Chunk.TsFrom,
                        item.Chunk.TsTo,
                        item.Match.Score))
                    .Take(resultLimit)
                    .ToList();

                stopwatch.Stop();
                AiPipelineLog.RetrievalCompleted(
                    logger,
                    request.QueryKind,
                    normalizedQuery.Length,
                    matches.Count,
                    chunkIds.Count,
                    retrievedChunks.Count,
                    stopwatch.ElapsedMilliseconds);

                try
                {
                    dbContext.RetrievalLogs.Add(new RetrievalLogEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = request.UserId,
                        QueryText = normalizedQuery,
                        QueryKind = request.QueryKind,
                        FiltersJson = SerializeFilters(request),
                        CandidateCount = matches.Count,
                        SelectedChunkIdsJson = JsonSerializer.Serialize(retrievedChunks.Select(item => item.ChunkId)),
                        LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                        ModelVersionsJson = JsonSerializer.Serialize(new Dictionary<string, object?>
                        {
                            ["embedding_provider"] = embedding.Provider,
                            ["embedding_model"] = embedding.Model,
                            ["embedding_version"] = string.IsNullOrWhiteSpace(embedding.EmbeddingVersion) ? null : embedding.EmbeddingVersion,
                            ["qdrant_collection"] = qdrantOptions.Value.MemoryCollectionName,
                            ["retrieval_mode"] = "hybrid_rrf_v1"
                        }),
                        CreatedAt = timeProvider.GetUtcNow()
                    });

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Failed to persist retrieval log for user {UserId}.", request.UserId);
                }

                return retrievedChunks;
            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            AiPipelineLog.RetrievalFailed(
                logger,
                request.QueryKind,
                normalizedQuery.Length,
                stopwatch.ElapsedMilliseconds,
                exception);
            throw;
        }
    }

    private static Guid? TryExtractChunkId(QdrantQueryPoint match)
    {
        if (TryParseGuid(match.Payload.TryGetValue("chunk_id", out var chunkIdValue) ? chunkIdValue : null, out var chunkId))
        {
            return chunkId;
        }

        return TryParseGuid(match.PointId, out chunkId)
            ? chunkId
            : null;
    }

    private static bool TryParseGuid(object? value, out Guid guid)
    {
        if (value is Guid typedGuid)
        {
            guid = typedGuid;
            return true;
        }

        if (value is string text && Guid.TryParse(text, out guid))
        {
            return true;
        }

        guid = Guid.Empty;
        return false;
    }

    private static string? SerializeFilters(RetrievalRequest request)
    {
        var filters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = request.UserId.ToString("D")
        };

        if (!string.IsNullOrWhiteSpace(request.ChatId))
        {
            filters["chat_id"] = request.ChatId;
        }

        if (!string.IsNullOrWhiteSpace(request.PeerId))
        {
            filters["peer_id"] = request.PeerId;
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            filters["kind"] = request.Kind;
        }

        return JsonSerializer.Serialize(filters);
    }
}
