using Microsoft.EntityFrameworkCore;

namespace SuperChat.Infrastructure.Shared.Persistence;

internal abstract class EfCoreRepository<TEntity>(
    IDbContextFactory<SuperChatDbContext> dbContextFactory) where TEntity : class
{
    protected async Task<SuperChatDbContext> GetDbContextAsync(CancellationToken cancellationToken)
        => await dbContextFactory.CreateDbContextAsync(cancellationToken);
}
