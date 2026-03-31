using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram;

internal sealed class EfTelegramConnectionRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<TelegramConnectionEntity>(dbContextFactory), ITelegramConnectionRepository
{
    public async Task<TelegramConnection?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.TelegramConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task SaveAsync(TelegramConnection connection, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.TelegramConnections
            .FirstOrDefaultAsync(t => t.UserId == connection.UserId, cancellationToken);

        if (entity is null)
        {
            entity = new TelegramConnectionEntity
            {
                UserId = connection.UserId,
                State = connection.State,
                WebLoginUrl = connection.WebLoginUrl?.AbsoluteUri,
                UpdatedAt = connection.UpdatedAt,
                LastSyncedAt = connection.LastSyncedAt
            };
            db.TelegramConnections.Add(entity);
        }
        else
        {
            entity.State = connection.State;
            entity.WebLoginUrl = connection.WebLoginUrl?.AbsoluteUri;
            entity.UpdatedAt = connection.UpdatedAt;
            entity.LastSyncedAt = connection.LastSyncedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelegramConnection>> GetConnectedUnsyncedAsync(CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.TelegramConnections
            .AsNoTracking()
            .Where(t => t.State == TelegramConnectionState.Connected && t.LastSyncedAt == null)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }
}
