using Microsoft.EntityFrameworkCore;

namespace SuperChat.Infrastructure.Shared.Persistence;

internal abstract class EfCoreRepository<TEntity>(
    IDbContextFactory<SuperChatDbContext> dbContextFactory) where TEntity : class
{
    protected async Task<SuperChatDbContext> GetDbContextAsync(CancellationToken cancellationToken)
        => await dbContextFactory.CreateDbContextAsync(cancellationToken);

    protected async Task<IReadOnlyList<TEntity>> ListAsync(
        IEfSpecification<TEntity> specification,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await GetDbContextAsync(cancellationToken);
        return await specification
            .Apply(dbContext.Set<TEntity>().AsNoTracking())
            .ToListAsync(cancellationToken);
    }

    protected async Task<TEntity?> FirstOrDefaultAsync(
        IEfSpecification<TEntity> specification,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await GetDbContextAsync(cancellationToken);
        return await specification
            .Apply(dbContext.Set<TEntity>().AsNoTracking())
            .FirstOrDefaultAsync(cancellationToken);
    }
}
