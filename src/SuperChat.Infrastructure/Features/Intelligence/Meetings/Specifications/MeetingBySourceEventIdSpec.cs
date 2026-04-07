using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings.Specifications;

internal sealed class MeetingBySourceEventIdSpec(Guid userId, string sourceEventId) : IEfSpecification<MeetingEntity>
{
    public IQueryable<MeetingEntity> Apply(IQueryable<MeetingEntity> query)
    {
        return query.Where(item => item.UserId == userId && item.SourceEventId == sourceEventId);
    }
}
