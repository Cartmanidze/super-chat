using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingAutoResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    ILogger<MeetingAutoResolutionService> logger)
{
    public async Task ResolveConversationAsync(
        Guid userId,
        string externalChatId,
        DateTimeOffset fromInclusive,
        CancellationToken cancellationToken)
    {
        await ResolveCoreAsync(userId, externalChatId, fromInclusive, dueBeforeInclusive: null, cancellationToken);
    }

    public async Task ResolveDueMeetingsAsync(
        Guid userId,
        string externalChatId,
        DateTimeOffset dueBeforeInclusive,
        CancellationToken cancellationToken)
    {
        await ResolveCoreAsync(userId, externalChatId, dueBeforeInclusive, dueBeforeInclusive, cancellationToken);
    }

    public async Task ResolveDueMeetingsAsync(
        DateTimeOffset dueBeforeInclusive,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var staleScopes = await dbContext.Meetings
            .AsNoTracking()
            .Where(item => item.ResolvedAt == null &&
                           item.ScheduledFor != null &&
                           item.ScheduledFor <= dueBeforeInclusive)
            .Select(item => new { item.UserId, item.SourceRoom })
            .Distinct()
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Loaded stale due-meeting scopes for background sweep. ScopeCount={ScopeCount}, DueBeforeInclusive={DueBeforeInclusive}.",
            staleScopes.Count,
            dueBeforeInclusive);

        foreach (var scope in staleScopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ResolveCoreAsync(scope.UserId, scope.SourceRoom, dueBeforeInclusive, dueBeforeInclusive, cancellationToken);
        }
    }

    private async Task ResolveCoreAsync(
        Guid userId,
        string? externalChatId,
        DateTimeOffset fromInclusive,
        DateTimeOffset? dueBeforeInclusive,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scheduledFrom = fromInclusive.AddDays(-1);
        var unresolvedMeetingsQuery = dbContext.Meetings
            .Where(item => item.UserId == userId &&
                           item.ResolvedAt == null);

        if (!string.IsNullOrWhiteSpace(externalChatId))
        {
            unresolvedMeetingsQuery = unresolvedMeetingsQuery.Where(item => item.SourceRoom == externalChatId);
        }

        if (dueBeforeInclusive is not null)
        {
            unresolvedMeetingsQuery = unresolvedMeetingsQuery.Where(item => item.ScheduledFor != null &&
                                                                           item.ScheduledFor <= dueBeforeInclusive.Value);
        }
        else
        {
            unresolvedMeetingsQuery = unresolvedMeetingsQuery.Where(item => item.ScheduledFor != null &&
                                                                           item.ScheduledFor <= fromInclusive);
        }

        var unresolvedMeetings = await unresolvedMeetingsQuery
            .ToListAsync(cancellationToken);

        var candidates = unresolvedMeetings
            .Where(item => item.ScheduledFor is not null && item.ScheduledFor >= scheduledFrom)
            .ToList();

        logger.LogInformation(
            "Loaded meeting auto-resolution candidates. CandidateCount={CandidateCount}, ExternalChatId={ExternalChatId}, DueBeforeInclusive={DueBeforeInclusive}.",
            candidates.Count,
            externalChatId ?? "(all)",
            dueBeforeInclusive);

        if (candidates.Count == 0)
        {
            return;
        }

        var roomIds = candidates
            .Select(item => item.SourceRoom)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var observedFrom = candidates.Min(item => item.ObservedAt);

        var messages = await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           roomIds.Contains(item.ExternalChatId))
            .ToListAsync(cancellationToken);

        var messagesByRoom = messages
            .Where(item => item.SentAt >= observedFrom)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.ReceivedAt)
            .GroupBy(item => item.ExternalChatId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<NormalizedMessageEntity>)group.ToList(), StringComparer.Ordinal);

        var changed = false;
        var resolvedCount = 0;
        foreach (var meeting in candidates)
        {
            var roomMessages = messagesByRoom.GetValueOrDefault(meeting.SourceRoom);
            if (roomMessages is null || roomMessages.Count == 0)
            {
                continue;
            }

            var laterMessages = roomMessages
                .Where(message => message.SentAt > meeting.ObservedAt ||
                                  (message.SentAt == meeting.ObservedAt &&
                                   !string.Equals(message.ExternalMessageId, meeting.SourceEventId, StringComparison.Ordinal)))
                .ToList();

            var resolution = WorkItemAutoResolutionDetector.TryResolve(meeting, laterMessages);
            if (resolution is null)
            {
                continue;
            }

            if (meeting.Status == MeetingStatus.Confirmed &&
                !string.Equals(resolution.ResolutionKind, WorkItemResolutionState.Completed, StringComparison.Ordinal) &&
                !string.Equals(resolution.ResolutionKind, WorkItemResolutionState.Cancelled, StringComparison.Ordinal))
            {
                continue;
            }

            meeting.ResolvedAt = resolution.ResolvedAt;
            meeting.ResolutionKind = resolution.ResolutionKind;
            meeting.ResolutionSource = resolution.ResolutionSource;
            meeting.UpdatedAt = DateTimeOffset.UtcNow;
            changed = true;
            resolvedCount++;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Completed meeting auto-resolution. CandidateCount={CandidateCount}, ResolvedCount={ResolvedCount}, MessageCount={MessageCount}.",
            candidates.Count,
            resolvedCount,
            messages.Count);
    }
}
