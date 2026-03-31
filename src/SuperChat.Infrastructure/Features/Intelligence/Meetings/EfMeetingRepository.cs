using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class EfMeetingRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<MeetingEntity>(dbContextFactory), IMeetingRepository
{
    public async Task<MeetingRecord?> FindByIdAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.Meetings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.UserId == userId, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<MeetingRecord?> FindBySourceEventIdAsync(Guid userId, string sourceEventId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.Meetings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.SourceEventId == sourceEventId, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<MeetingRecord>> GetUnresolvedAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.Meetings
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.ResolvedAt == null)
            .OrderByDescending(m => m.ObservedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(Guid userId, DateTimeOffset from, int take, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.Meetings
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.ResolvedAt == null && m.ScheduledFor >= from)
            .OrderBy(m => m.ScheduledFor)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task UpsertRangeAsync(IReadOnlyList<MeetingRecord> meetings, CancellationToken cancellationToken)
    {
        if (meetings.Count == 0) return;

        await using var db = await GetDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var meeting in meetings)
        {
            var entity = await db.Meetings
                .FirstOrDefaultAsync(m => m.UserId == meeting.UserId && m.SourceEventId == meeting.SourceEventId, cancellationToken);

            if (entity is null)
            {
                entity = new MeetingEntity
                {
                    Id = meeting.Id,
                    UserId = meeting.UserId,
                    Title = meeting.Title,
                    Summary = meeting.Summary,
                    SourceRoom = meeting.SourceRoom,
                    SourceEventId = meeting.SourceEventId,
                    Person = meeting.Person,
                    ObservedAt = meeting.ObservedAt,
                    ScheduledFor = meeting.ScheduledFor,
                    Confidence = meeting.Confidence.Value,
                    MeetingProvider = meeting.MeetingProvider,
                    MeetingJoinUrl = meeting.MeetingJoinUrl?.AbsoluteUri,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Meetings.Add(entity);
            }
            else
            {
                entity.Title = meeting.Title;
                entity.Summary = meeting.Summary;
                entity.SourceRoom = meeting.SourceRoom;
                entity.Person = meeting.Person;
                entity.ObservedAt = meeting.ObservedAt;
                entity.ScheduledFor = meeting.ScheduledFor;
                entity.Confidence = meeting.Confidence.Value;
                entity.MeetingProvider = meeting.MeetingProvider;
                entity.MeetingJoinUrl = meeting.MeetingJoinUrl?.AbsoluteUri;
                entity.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAsync(Guid meetingId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, cancellationToken);

        if (entity is null) return;

        entity.ResolvedAt = resolvedAt;
        entity.ResolutionKind = resolutionKind;
        entity.ResolutionSource = resolutionSource;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
