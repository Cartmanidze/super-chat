using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Auth;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Auth;

internal sealed class EfAppUserRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<AppUserEntity>(dbContextFactory), IAppUserRepository
{
    public async Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = new Email(email).Value;
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<AppUser> CreateOrRefreshAsync(string email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = new Email(email).Value;
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (entity is null)
        {
            entity = new AppUserEntity
            {
                Id = Guid.NewGuid(),
                Email = normalized,
                CreatedAt = now,
                LastSeenAt = now
            };
            db.AppUsers.Add(entity);
        }
        else
        {
            entity.LastSeenAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }
}
