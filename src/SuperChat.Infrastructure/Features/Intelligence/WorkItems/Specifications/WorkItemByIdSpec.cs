using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems.Specifications;

internal sealed class WorkItemByIdSpec(Guid userId, Guid workItemId) : IEfSpecification<WorkItemEntity>
{
    public IQueryable<WorkItemEntity> Apply(IQueryable<WorkItemEntity> query)
    {
        return query.Where(item => item.UserId == userId && item.Id == workItemId);
    }
}
