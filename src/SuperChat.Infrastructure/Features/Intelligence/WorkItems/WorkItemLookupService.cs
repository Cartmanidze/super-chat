using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class WorkItemLookupService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public async Task<WorkItemRecord?> GetByIdAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.WorkItems
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == workItemId, cancellationToken);

        return entity?.ToDomain();
    }
}
