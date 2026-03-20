using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

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
