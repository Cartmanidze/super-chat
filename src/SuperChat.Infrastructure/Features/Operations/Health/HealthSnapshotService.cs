using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class HealthSnapshotService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    TimeProvider timeProvider) : IHealthSnapshotService
{
    public async Task<HealthSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var invitedUsers = await db.PilotInvites.CountAsync(item => item.IsActive, cancellationToken);
        var knownUsers = await db.AppUsers.CountAsync(cancellationToken);
        var pendingMessages = await db.NormalizedMessages.CountAsync(item => !item.Processed, cancellationToken);
        var extractedItems = await db.WorkItems.CountAsync(cancellationToken) +
                             await db.Meetings.CountAsync(cancellationToken);
        var sessionExpirations = await db.ApiSessions
            .AsNoTracking()
            .Select(item => item.ExpiresAt)
            .ToListAsync(cancellationToken);

        var activeSessions = sessionExpirations.Count(expiresAt => expiresAt > now);

        return new HealthSnapshot(
            invitedUsers,
            knownUsers,
            pendingMessages,
            extractedItems,
            activeSessions);
    }
}
