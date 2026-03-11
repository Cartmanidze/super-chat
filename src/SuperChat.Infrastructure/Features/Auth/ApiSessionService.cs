using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class ApiSessionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    PilotOptions pilotOptions,
    TimeProvider timeProvider) : IApiSessionService
{
    public async Task<ApiSession> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var entity = new ApiSessionEntity
        {
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            ExpiresAt = now.AddDays(pilotOptions.ApiSessionDays)
        };

        dbContext.ApiSessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    public async Task<AppUser?> GetUserAsync(string token, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var session = await dbContext.ApiSessions
            .SingleOrDefaultAsync(item => item.Token == token, cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.ExpiresAt <= now)
        {
            dbContext.ApiSessions.Remove(session);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var user = await dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.UserId, cancellationToken);

        return user?.ToDomain();
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ApiSessions
            .Where(item => item.Token == token)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
