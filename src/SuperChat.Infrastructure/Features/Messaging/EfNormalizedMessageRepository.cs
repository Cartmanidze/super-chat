using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Messaging;

internal sealed class EfNormalizedMessageRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<NormalizedMessageEntity>(dbContextFactory), INormalizedMessageRepository
{
    public async Task<bool> ExistsAsync(Guid userId, string matrixRoomId, string matrixEventId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        return await db.NormalizedMessages
            .AnyAsync(m => m.UserId == userId && m.MatrixRoomId == matrixRoomId && m.MatrixEventId == matrixEventId, cancellationToken);
    }

    public async Task AddAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        db.NormalizedMessages.Add(new NormalizedMessageEntity
        {
            Id = message.Id,
            UserId = message.UserId,
            Source = message.Source,
            MatrixRoomId = message.MatrixRoomId,
            MatrixEventId = message.MatrixEventId,
            SenderName = message.SenderName,
            Text = message.Text,
            SentAt = message.SentAt,
            IngestedAt = message.IngestedAt,
            Processed = message.Processed
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetPendingAsync(CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.NormalizedMessages
            .AsNoTracking()
            .Where(m => !m.Processed)
            .OrderBy(m => m.IngestedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetPendingForConversationAsync(Guid userId, string source, string matrixRoomId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.NormalizedMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Source == source && m.MatrixRoomId == matrixRoomId && !m.Processed)
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.NormalizedMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task MarkProcessedAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0) return;

        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.NormalizedMessages
            .Where(m => messageIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            entity.Processed = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
