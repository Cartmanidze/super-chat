using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class ExtractedItemLookupService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public async Task<ExtractedItem?> GetByIdAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.ExtractedItems
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == itemId, cancellationToken);

        return entity?.ToDomain();
    }
}
