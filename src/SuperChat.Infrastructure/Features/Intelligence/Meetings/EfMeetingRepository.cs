using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings.Specifications;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class EfMeetingRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<MeetingEntity>(dbContextFactory), IMeetingRepository
{
    public async Task<MeetingRecord?> FindByIdAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        var entity = await FirstOrDefaultAsync(new MeetingByIdSpec(userId, meetingId), cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<MeetingRecord?> FindBySourceEventIdAsync(Guid userId, string sourceEventId, CancellationToken cancellationToken)
    {
        var entity = await FirstOrDefaultAsync(new MeetingBySourceEventIdSpec(userId, sourceEventId), cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<MeetingRecord>> GetUnresolvedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await ListAsync(new UnresolvedMeetingsSpec(userId), cancellationToken);

        return entities
            .OrderByDescending(item => item.ObservedAt)
            .Select(entity => entity.ToDomain())
            .ToList();
    }

    public async Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(Guid userId, DateTimeOffset from, int take, CancellationToken cancellationToken)
    {
        var boundedTake = Math.Clamp(take, 1, 50);
        var fromInclusiveUtc = MeetingTimeSupport.NormalizeToUtc(from);
        var entities = await ListAsync(new UnresolvedMeetingsSpec(userId), cancellationToken);

        return entities
            .Where(item => item.ScheduledFor is not null && item.ScheduledFor >= fromInclusiveUtc)
            .OrderBy(item => item.Status == MeetingStatus.Confirmed ? 0 : 1)
            .ThenBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Take(Math.Max(50, boundedTake * 4))
            .Select(entity => entity.ToDomain())
            .GroupBy(item => item.ToMeetingDeduplicationKey(), StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.Status == MeetingStatus.Confirmed ? 0 : 1)
                .ThenBy(item => item.ScheduledFor)
                .ThenByDescending(item => item.Confidence.Value)
                .ThenByDescending(item => item.ObservedAt)
                .First())
            .OrderBy(item => item.Status == MeetingStatus.Confirmed ? 0 : 1)
            .ThenBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence.Value)
            .Take(boundedTake)
            .ToList();
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
                    Status = meeting.Status,
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
                entity.Status = meeting.Status;
                entity.MeetingProvider = meeting.MeetingProvider;
                entity.MeetingJoinUrl = meeting.MeetingJoinUrl?.AbsoluteUri;
                entity.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid userId,
        Guid meetingId,
        MeetingStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entity = await db.Meetings
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Id == meetingId, cancellationToken);

        if (entity is null || entity.IsResolved())
        {
            return;
        }

        entity.Status = status;
        entity.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveRelatedAsync(
        Guid userId,
        string sourceEventId,
        string resolutionKind,
        string resolutionSource,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        var entities = await db.Meetings
            .Where(item => item.UserId == userId && item.SourceEventId == sourceEventId)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var entity in entities)
        {
            changed |= ApplyResolution(entity, resolutionKind, resolutionSource, resolvedAt);
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
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

    private static bool ApplyResolution(
        MeetingEntity entity,
        string resolutionKind,
        string resolutionSource,
        DateTimeOffset resolvedAt)
    {
        if (entity.IsResolved() &&
            string.Equals(entity.ResolutionKind, resolutionKind, StringComparison.Ordinal) &&
            string.Equals(entity.ResolutionSource, resolutionSource, StringComparison.Ordinal))
        {
            return false;
        }

        entity.ResolvedAt ??= resolvedAt;
        entity.ResolutionKind = resolutionKind;
        entity.ResolutionSource = resolutionSource;
        entity.UpdatedAt = resolvedAt;
        return true;
    }
}
