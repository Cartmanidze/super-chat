using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingLookupService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public async Task<MeetingRecord?> GetByIdAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Meetings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == meetingId, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<MeetingRecord?> GetBySourceEventIdAsync(Guid userId, string sourceEventId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Meetings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.SourceEventId == sourceEventId, cancellationToken);

        return entity?.ToDomain();
    }
}
