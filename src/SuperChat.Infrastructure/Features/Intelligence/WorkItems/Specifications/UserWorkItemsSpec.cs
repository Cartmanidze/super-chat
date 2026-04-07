using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems.Specifications;

internal sealed class UserWorkItemsSpec(Guid userId, bool unresolvedOnly) : IEfSpecification<WorkItemEntity>
{
    public IQueryable<WorkItemEntity> Apply(IQueryable<WorkItemEntity> query)
    {
        query = query.Where(item => item.UserId == userId);

        if (unresolvedOnly)
        {
            query = query.Where(item => item.ResolvedAt == null);
        }

        return query;
    }
}
