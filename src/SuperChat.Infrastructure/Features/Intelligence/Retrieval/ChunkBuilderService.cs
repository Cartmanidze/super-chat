using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

public sealed class ChunkBuilderService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IOptions<ChunkingOptions> chunkingOptions,
    TimeProvider timeProvider) : IChunkBuilderService
{
    public async Task<ChunkBuildRunResult> BuildPendingChunksAsync(CancellationToken cancellationToken)
    {
        var options = chunkingOptions.Value;
        if (!options.Enabled)
        {
            return ChunkBuildRunResult.Empty;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var checkpoints = await dbContext.ChunkBuildCheckpoints
            .AsNoTracking()
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var latestMessagesByUser = await dbContext.NormalizedMessages
            .AsNoTracking()
            .GroupBy(item => item.UserId)
            .Select(group => new UserLatestIngested(group.Key, group.Max(item => item.ReceivedAt)))
            .ToListAsync(cancellationToken);

        var usersToProcess = latestMessagesByUser
            .Where(item => ShouldProcessUser(item.LatestReceivedAt, checkpoints.GetValueOrDefault(item.UserId)))
            .Select(item => item.UserId)
            .ToList();

        var aggregate = ChunkBuildRunResult.Empty;
        foreach (var userId in usersToProcess)
        {
            aggregate = aggregate.Merge(await ProcessUserAsync(
                userId,
                checkpoints.GetValueOrDefault(userId),
                options,
                cancellationToken));
        }

        return aggregate;
    }

    public async Task<ChunkBuildRunResult> BuildConversationChunksAsync(
        Guid userId,
        string externalChatId,
        DateTimeOffset rebuildFrom,
        CancellationToken cancellationToken)
    {
        var options = chunkingOptions.Value;
        if (!options.Enabled)
        {
            return ChunkBuildRunResult.Empty;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await RebuildRoomAsync(
            dbContext,
            userId,
            externalChatId,
            rebuildFrom,
            options,
            timeProvider.GetUtcNow(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    internal static bool ShouldProcessUser(DateTimeOffset latestReceivedAt, ChunkBuildCheckpointEntity? checkpoint)
    {
        return checkpoint?.LastObservedReceivedAt is not DateTimeOffset lastObservedReceivedAt ||
               latestReceivedAt >= lastObservedReceivedAt;
    }

    internal static IReadOnlyList<NormalizedMessageEntity> FilterNewMessages(
        IReadOnlyList<NormalizedMessageEntity> candidateMessages,
        ChunkBuildCheckpointEntity? checkpoint)
    {
        if (checkpoint?.LastObservedReceivedAt is not DateTimeOffset lastObservedReceivedAt)
        {
            return candidateMessages;
        }

        return candidateMessages
            .Where(item => item.ReceivedAt > lastObservedReceivedAt ||
                           (item.ReceivedAt == lastObservedReceivedAt &&
                            (checkpoint.LastObservedMessageId is null || item.Id.CompareTo(checkpoint.LastObservedMessageId.Value) > 0)))
            .ToList();
    }

    internal static IReadOnlyList<MessageChunkEntity> BuildChunkEntities(
        Guid userId,
        string roomId,
        IReadOnlyList<NormalizedMessage> messages,
        ChunkingOptions options,
        DateTimeOffset now)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var orderedMessages = messages
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .ToList();

        var maxGap = TimeSpan.FromMinutes(Math.Max(1, options.MaxGapMinutes));
        var maxMessagesPerChunk = Math.Max(1, options.MaxMessagesPerChunk);
        var maxChunkCharacters = Math.Max(200, options.MaxChunkCharacters);

        var chunks = new List<MessageChunkEntity>();
        var buffer = new List<NormalizedMessage>();
        var currentLength = 0;

        foreach (var message in orderedMessages)
        {
            var renderedMessage = RenderMessageLine(message);
            if (buffer.Count > 0)
            {
                var previousMessage = buffer[^1];
                var gapTooLarge = message.SentAt - previousMessage.SentAt > maxGap;
                var messageLimitReached = buffer.Count >= maxMessagesPerChunk;
                var projectedLength = currentLength + 1 + renderedMessage.Length;
                var characterLimitReached = projectedLength > maxChunkCharacters;

                if (gapTooLarge || messageLimitReached || characterLimitReached)
                {
                    chunks.Add(CreateChunkEntity(userId, roomId, buffer, now));
                    buffer.Clear();
                    currentLength = 0;
                }
            }

            buffer.Add(message);
            currentLength = currentLength == 0
                ? renderedMessage.Length
                : currentLength + 1 + renderedMessage.Length;
        }

        if (buffer.Count > 0)
        {
            chunks.Add(CreateChunkEntity(userId, roomId, buffer, now));
        }

        return chunks;
    }

    private async Task<ChunkBuildRunResult> ProcessUserAsync(
        Guid userId,
        ChunkBuildCheckpointEntity? checkpoint,
        ChunkingOptions options,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var checkpointBoundary = checkpoint?.LastObservedReceivedAt ?? DateTimeOffset.MinValue;
        var candidateMessages = await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.ReceivedAt >= checkpointBoundary)
            .OrderBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var newMessages = FilterNewMessages(candidateMessages, checkpoint);
        if (newMessages.Count == 0)
        {
            return ChunkBuildRunResult.Empty;
        }

        var roomPlans = newMessages
            .GroupBy(item => item.ExternalChatId)
            .Select(group => new RoomRebuildPlan(
                group.Key,
                group.Min(item => item.SentAt).AddMinutes(-Math.Max(1, options.MaxGapMinutes))))
            .ToList();

        var now = timeProvider.GetUtcNow();
        var roomsRebuilt = 0;
        var chunksWritten = 0;
        var messagesConsidered = 0;

        foreach (var roomPlan in roomPlans)
        {
            var roomMessages = await dbContext.NormalizedMessages
                .AsNoTracking()
                .Where(item => item.UserId == userId &&
                               item.ExternalChatId == roomPlan.RoomId &&
                               item.SentAt >= roomPlan.RebuildFrom)
                .OrderBy(item => item.SentAt)
                .ThenBy(item => item.ReceivedAt)
                .ThenBy(item => item.Id)
                .Select(item => item.ToDomain())
                .ToListAsync(cancellationToken);

            var overlappingChunks = await dbContext.MessageChunks
                .Where(item => item.UserId == userId &&
                               item.ChatId == roomPlan.RoomId &&
                               item.TsTo >= roomPlan.RebuildFrom)
                .ToListAsync(cancellationToken);

            if (overlappingChunks.Count > 0)
            {
                dbContext.MessageChunks.RemoveRange(overlappingChunks);
            }

            if (roomMessages.Count == 0)
            {
                continue;
            }

            var rebuiltChunks = BuildChunkEntities(userId, roomPlan.RoomId, roomMessages, options, now);
            dbContext.MessageChunks.AddRange(rebuiltChunks);

            roomsRebuilt++;
            chunksWritten += rebuiltChunks.Count;
            messagesConsidered += roomMessages.Count;
        }

        var storedCheckpoint = await dbContext.ChunkBuildCheckpoints
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (storedCheckpoint is null)
        {
            storedCheckpoint = new ChunkBuildCheckpointEntity
            {
                UserId = userId
            };

            dbContext.ChunkBuildCheckpoints.Add(storedCheckpoint);
        }

        var lastMessage = newMessages
            .OrderBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .Last();

        storedCheckpoint.LastObservedReceivedAt = lastMessage.ReceivedAt;
        storedCheckpoint.LastObservedMessageId = lastMessage.Id;
        storedCheckpoint.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChunkBuildRunResult(
            1,
            roomsRebuilt,
            chunksWritten,
            messagesConsidered);
    }

    private static async Task<ChunkBuildRunResult> RebuildRoomAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string roomId,
        DateTimeOffset rebuildFrom,
        ChunkingOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var roomMessages = await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.ExternalChatId == roomId &&
                           item.SentAt >= rebuildFrom)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);

        var overlappingChunks = await dbContext.MessageChunks
            .Where(item => item.UserId == userId &&
                           item.ChatId == roomId &&
                           item.TsTo >= rebuildFrom)
            .ToListAsync(cancellationToken);

        if (overlappingChunks.Count > 0)
        {
            dbContext.MessageChunks.RemoveRange(overlappingChunks);
        }

        if (roomMessages.Count == 0)
        {
            return overlappingChunks.Count > 0
                ? new ChunkBuildRunResult(1, 1, 0, 0)
                : ChunkBuildRunResult.Empty;
        }

        var rebuiltChunks = BuildChunkEntities(userId, roomId, roomMessages, options, now);
        dbContext.MessageChunks.AddRange(rebuiltChunks);

        return new ChunkBuildRunResult(
            1,
            1,
            rebuiltChunks.Count,
            roomMessages.Count);
    }

    private static MessageChunkEntity CreateChunkEntity(
        Guid userId,
        string roomId,
        IReadOnlyList<NormalizedMessage> messages,
        DateTimeOffset now)
    {
        var firstMessage = messages[0];
        var lastMessage = messages[^1];
        var text = string.Join('\n', messages.Select(RenderMessageLine));

        return new MessageChunkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = firstMessage.Source,
            Provider = firstMessage.Source,
            Transport = "matrix_bridge",
            ChatId = roomId,
            PeerId = null,
            ThreadId = null,
            Kind = "dialog_chunk",
            Text = text,
            MessageCount = messages.Count,
            FirstNormalizedMessageId = firstMessage.Id,
            LastNormalizedMessageId = lastMessage.Id,
            TsFrom = firstMessage.SentAt,
            TsTo = lastMessage.SentAt,
            ContentHash = ComputeContentHash(roomId, messages),
            ChunkVersion = 1,
            EmbeddingVersion = null,
            QdrantPointId = null,
            IndexedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string RenderMessageLine(NormalizedMessage message)
    {
        return $"{message.SenderName}: {message.Text.Trim()}";
    }

    private static string ComputeContentHash(string roomId, IReadOnlyList<NormalizedMessage> messages)
    {
        var lines = new List<string>(messages.Count + 1)
        {
            roomId
        };

        lines.AddRange(messages.Select(item => $"{item.Id:N}|{item.SentAt.UtcTicks}|{item.SenderName}|{item.Text}"));

        var payload = string.Join('\n', lines);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record UserLatestIngested(Guid UserId, DateTimeOffset LatestReceivedAt);

    private sealed record RoomRebuildPlan(string RoomId, DateTimeOffset RebuildFrom);
}
