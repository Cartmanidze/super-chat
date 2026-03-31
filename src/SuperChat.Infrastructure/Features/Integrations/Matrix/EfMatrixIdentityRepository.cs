using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Integrations.Matrix;

internal sealed class EfMatrixIdentityRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<MatrixIdentityEntity>(dbContextFactory), IMatrixIdentityRepository
{
    public async Task<MatrixIdentity?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.MatrixIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task SaveAsync(MatrixIdentity identity, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.MatrixIdentities
            .FirstOrDefaultAsync(m => m.UserId == identity.UserId, cancellationToken);

        if (entity is null)
        {
            entity = new MatrixIdentityEntity
            {
                UserId = identity.UserId,
                MatrixUserId = identity.MatrixUserId,
                AccessToken = identity.AccessToken,
                ProvisionedAt = identity.ProvisionedAt
            };
            db.MatrixIdentities.Add(entity);
        }
        else
        {
            entity.MatrixUserId = identity.MatrixUserId;
            entity.AccessToken = identity.AccessToken;
            entity.ProvisionedAt = identity.ProvisionedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
