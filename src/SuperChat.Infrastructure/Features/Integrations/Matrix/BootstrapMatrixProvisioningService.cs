using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class BootstrapMatrixProvisioningService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MatrixOptions options,
    TimeProvider timeProvider) : IMatrixProvisioningService
{
    public async Task<MatrixIdentity> EnsureIdentityAsync(AppUser user, CancellationToken cancellationToken)
    {
        var existing = await GetIdentityAsync(user.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var server = new Uri(options.HomeserverUrl).Host;
        var slug = BuildSlug(user.Email);
        var entity = new MatrixIdentityEntity
        {
            UserId = user.Id,
            MatrixUserId = $"@{options.UserIdPrefix}-{slug}:{server}",
            AccessToken = $"dev-token-{user.Id:N}",
            ProvisionedAt = timeProvider.GetUtcNow()
        };

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.MatrixIdentities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return entity.ToDomain();
    }

    public async Task<MatrixIdentity?> GetIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var identity = await db.MatrixIdentities.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return identity?.ToDomain();
    }

    private static string BuildSlug(string email)
    {
        return email.Split('@')[0]
            .Replace(".", "-")
            .Replace("_", "-");
    }
}
