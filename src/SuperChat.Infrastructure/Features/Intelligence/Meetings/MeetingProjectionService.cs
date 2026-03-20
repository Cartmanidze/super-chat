using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

public sealed class MeetingProjectionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IOptions<MeetingProjectionOptions> meetingProjectionOptions,
    PilotOptions pilotOptions,
    TimeProvider timeProvider) : IMeetingProjectionService
{
    public async Task<MeetingProjectionRunResult> ProjectPendingChunkMeetingsAsync(CancellationToken cancellationToken)
    {
        var options = meetingProjectionOptions.Value;
        if (!options.Enabled)
        {
            return MeetingProjectionRunResult.Empty;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var checkpoints = await dbContext.MeetingProjectionCheckpoints
            .AsNoTracking()
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var latestChunksByUser = await dbContext.MessageChunks
            .AsNoTracking()
            .GroupBy(item => item.UserId)
            .Select(group => new UserLatestChunk(group.Key, group.Max(item => item.UpdatedAt)))
            .ToListAsync(cancellationToken);

        var usersToProcess = latestChunksByUser
            .Where(item => ShouldProcessUser(item.LatestUpdatedAt, checkpoints.GetValueOrDefault(item.UserId)))
            .Select(item => item.UserId)
            .ToList();

        var aggregate = MeetingProjectionRunResult.Empty;
        foreach (var userId in usersToProcess)
        {
            aggregate = aggregate.Merge(await ProcessUserAsync(
                userId,
                checkpoints.GetValueOrDefault(userId),
                cancellationToken));
        }

        return aggregate;
    }

    public async Task<MeetingProjectionRunResult> ProjectConversationMeetingsAsync(
        Guid userId,
        string matrixRoomId,
        CancellationToken cancellationToken)
    {
        var options = meetingProjectionOptions.Value;
        if (!options.Enabled)
        {
            return MeetingProjectionRunResult.Empty;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await RebuildRoomMeetingsAsync(
            dbContext,
            userId,
            matrixRoomId,
            MeetingTimeSupport.ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId),
            timeProvider.GetUtcNow(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    internal static bool ShouldProcessUser(DateTimeOffset latestUpdatedAt, MeetingProjectionCheckpointEntity? checkpoint)
    {
        return checkpoint?.LastObservedChunkUpdatedAt is not DateTimeOffset lastObservedChunkUpdatedAt ||
               latestUpdatedAt >= lastObservedChunkUpdatedAt;
    }

    internal static IReadOnlyList<MessageChunkEntity> FilterNewChunks(
        IReadOnlyList<MessageChunkEntity> candidateChunks,
        MeetingProjectionCheckpointEntity? checkpoint)
    {
        if (checkpoint?.LastObservedChunkUpdatedAt is not DateTimeOffset lastObservedChunkUpdatedAt)
        {
            return candidateChunks;
        }

        return candidateChunks
            .Where(item => item.UpdatedAt > lastObservedChunkUpdatedAt ||
                           (item.UpdatedAt == lastObservedChunkUpdatedAt &&
                            (checkpoint.LastObservedChunkId is null || item.Id.CompareTo(checkpoint.LastObservedChunkId.Value) > 0)))
            .ToList();
    }

    private async Task<MeetingProjectionRunResult> ProcessUserAsync(
        Guid userId,
        MeetingProjectionCheckpointEntity? checkpoint,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var checkpointBoundary = checkpoint?.LastObservedChunkUpdatedAt ?? DateTimeOffset.MinValue;
        var candidateChunks = await dbContext.MessageChunks
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.UpdatedAt >= checkpointBoundary)
            .OrderBy(item => item.UpdatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var newChunks = FilterNewChunks(candidateChunks, checkpoint);
        if (newChunks.Count == 0)
        {
            return MeetingProjectionRunResult.Empty;
        }

        var referenceTimeZone = MeetingTimeSupport.ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var now = timeProvider.GetUtcNow();
        var roomsRebuilt = 0;
        var meetingsProjected = 0;

        var changedRoomIds = newChunks
            .Select(item => item.ChatId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var roomId in changedRoomIds)
        {
            var roomChunks = await dbContext.MessageChunks
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.ChatId == roomId)
                .OrderBy(item => item.TsFrom)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

            var projectedMeetings = roomChunks
                .Select(item => item.ToMeetingCandidate(referenceTimeZone))
                .OfType<MeetingRecord>()
                .GroupBy(item => item.ToMeetingDeduplicationKey(), StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(item => item.Confidence)
                    .ThenByDescending(item => item.ObservedAt)
                    .ThenBy(item => item.ScheduledFor)
                    .First())
                .ToList();

            var existingChunkMeetings = await dbContext.Meetings
                .Where(item => item.UserId == userId &&
                               item.SourceRoom == roomId &&
                               EF.Functions.Like(item.SourceEventId, "chunk:%"))
                .ToListAsync(cancellationToken);

            var existingBySourceEventId = existingChunkMeetings.ToDictionary(
                item => item.SourceEventId,
                item => item,
                StringComparer.Ordinal);

            var retainedSourceEventIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var meeting in projectedMeetings)
            {
                retainedSourceEventIds.Add(meeting.SourceEventId);

                if (existingBySourceEventId.TryGetValue(meeting.SourceEventId, out var existing))
                {
                    existing.Title = meeting.Title;
                    existing.Summary = meeting.Summary;
                    existing.Person = meeting.Person;
                    existing.ObservedAt = MeetingTimeSupport.NormalizeToUtc(meeting.ObservedAt);
                    existing.ScheduledFor = MeetingTimeSupport.NormalizeToUtc(meeting.ScheduledFor);
                    existing.Confidence = meeting.Confidence;
                    existing.MeetingProvider = meeting.MeetingProvider;
                    existing.MeetingJoinUrl = meeting.MeetingJoinUrl?.ToString();
                    existing.UpdatedAt = now;
                    continue;
                }

                dbContext.Meetings.Add(new MeetingEntity
                {
                    Id = meeting.Id,
                    UserId = meeting.UserId,
                    Title = meeting.Title,
                    Summary = meeting.Summary,
                    SourceRoom = meeting.SourceRoom,
                    SourceEventId = meeting.SourceEventId,
                    Person = meeting.Person,
                    ObservedAt = MeetingTimeSupport.NormalizeToUtc(meeting.ObservedAt),
                    ScheduledFor = MeetingTimeSupport.NormalizeToUtc(meeting.ScheduledFor),
                    Confidence = meeting.Confidence,
                    MeetingProvider = meeting.MeetingProvider,
                    MeetingJoinUrl = meeting.MeetingJoinUrl?.ToString(),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            var staleMeetings = existingChunkMeetings
                .Where(item => !retainedSourceEventIds.Contains(item.SourceEventId))
                .ToList();

            if (staleMeetings.Count > 0)
            {
                dbContext.Meetings.RemoveRange(staleMeetings);
            }

            roomsRebuilt++;
            meetingsProjected += projectedMeetings.Count;
        }

        var storedCheckpoint = await dbContext.MeetingProjectionCheckpoints
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (storedCheckpoint is null)
        {
            storedCheckpoint = new MeetingProjectionCheckpointEntity
            {
                UserId = userId
            };

            dbContext.MeetingProjectionCheckpoints.Add(storedCheckpoint);
        }

        var lastChunk = newChunks
            .OrderBy(item => item.UpdatedAt)
            .ThenBy(item => item.Id)
            .Last();

        storedCheckpoint.LastObservedChunkUpdatedAt = lastChunk.UpdatedAt;
        storedCheckpoint.LastObservedChunkId = lastChunk.Id;
        storedCheckpoint.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new MeetingProjectionRunResult(
            1,
            roomsRebuilt,
            meetingsProjected);
    }

    private static async Task<MeetingProjectionRunResult> RebuildRoomMeetingsAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string roomId,
        TimeZoneInfo referenceTimeZone,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var roomChunks = await dbContext.MessageChunks
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.ChatId == roomId)
            .OrderBy(item => item.TsFrom)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var projectedMeetings = roomChunks
            .Select(item => item.ToMeetingCandidate(referenceTimeZone))
            .OfType<MeetingRecord>()
            .GroupBy(item => item.ToMeetingDeduplicationKey(), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.Confidence)
                .ThenByDescending(item => item.ObservedAt)
                .ThenBy(item => item.ScheduledFor)
                .First())
            .ToList();

        var existingChunkMeetings = await dbContext.Meetings
            .Where(item => item.UserId == userId &&
                           item.SourceRoom == roomId &&
                           EF.Functions.Like(item.SourceEventId, "chunk:%"))
            .ToListAsync(cancellationToken);

        var existingBySourceEventId = existingChunkMeetings.ToDictionary(
            item => item.SourceEventId,
            item => item,
            StringComparer.Ordinal);

        var retainedSourceEventIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var meeting in projectedMeetings)
        {
            retainedSourceEventIds.Add(meeting.SourceEventId);

            if (existingBySourceEventId.TryGetValue(meeting.SourceEventId, out var existing))
            {
                existing.Title = meeting.Title;
                existing.Summary = meeting.Summary;
                existing.Person = meeting.Person;
                existing.ObservedAt = MeetingTimeSupport.NormalizeToUtc(meeting.ObservedAt);
                existing.ScheduledFor = MeetingTimeSupport.NormalizeToUtc(meeting.ScheduledFor);
                existing.Confidence = meeting.Confidence;
                existing.MeetingProvider = meeting.MeetingProvider;
                existing.MeetingJoinUrl = meeting.MeetingJoinUrl?.ToString();
                existing.UpdatedAt = now;
                continue;
            }

            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = meeting.Id,
                UserId = meeting.UserId,
                Title = meeting.Title,
                Summary = meeting.Summary,
                SourceRoom = meeting.SourceRoom,
                SourceEventId = meeting.SourceEventId,
                Person = meeting.Person,
                ObservedAt = MeetingTimeSupport.NormalizeToUtc(meeting.ObservedAt),
                ScheduledFor = MeetingTimeSupport.NormalizeToUtc(meeting.ScheduledFor),
                Confidence = meeting.Confidence,
                MeetingProvider = meeting.MeetingProvider,
                MeetingJoinUrl = meeting.MeetingJoinUrl?.ToString(),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var staleMeetings = existingChunkMeetings
            .Where(item => !retainedSourceEventIds.Contains(item.SourceEventId))
            .ToList();

        if (staleMeetings.Count > 0)
        {
            dbContext.Meetings.RemoveRange(staleMeetings);
        }

        return roomChunks.Count == 0 && existingChunkMeetings.Count == 0
            ? MeetingProjectionRunResult.Empty
            : new MeetingProjectionRunResult(1, 1, projectedMeetings.Count);
    }

    private sealed record UserLatestChunk(Guid UserId, DateTimeOffset LatestUpdatedAt);
}
