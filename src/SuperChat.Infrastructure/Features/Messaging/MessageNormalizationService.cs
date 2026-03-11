using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class MessageNormalizationService(IDbContextFactory<SuperChatDbContext> dbContextFactory) : IMessageNormalizationService
{
    public async Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => !item.Processed)
            .OrderBy(item => item.SentAt)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SentAt)
            .Take(take)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
    {
        var ids = messageIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var messages = await dbContext.NormalizedMessages
            .Where(item => ids.Contains(item.Id))
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.Processed = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryStoreAsync(
        Guid userId,
        string roomId,
        string eventId,
        string senderName,
        string text,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await dbContext.NormalizedMessages
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.MatrixRoomId == roomId && item.MatrixEventId == eventId, cancellationToken);

        if (exists)
        {
            return false;
        }

        dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = "telegram",
            MatrixRoomId = roomId,
            MatrixEventId = eventId,
            SenderName = senderName,
            Text = text,
            SentAt = sentAt,
            IngestedAt = DateTimeOffset.UtcNow,
            Processed = false
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
