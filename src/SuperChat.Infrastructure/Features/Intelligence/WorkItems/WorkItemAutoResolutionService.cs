using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class WorkItemAutoResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    ILogger<WorkItemAutoResolutionService> logger,
    TimeProvider timeProvider,
    IOptions<ResolutionOptions> resolutionOptions)
{
    public async Task ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await ResolveCoreAsync(userId, externalChatId: null, cancellationToken);
    }

    public async Task ResolveConversationAsync(
        Guid userId,
        string externalChatId,
        CancellationToken cancellationToken)
    {
        await ResolveCoreAsync(userId, externalChatId, cancellationToken);
    }

    private async Task ResolveCoreAsync(
        Guid userId,
        string? externalChatId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var cooldownThreshold = timeProvider.GetUtcNow()
            .AddMinutes(-resolutionOptions.Value.AutoResolutionCooldownMinutes);
        var candidatesQuery = dbContext.WorkItems
            .Where(item => item.UserId == userId &&
                           item.ResolvedAt == null &&
                           item.ObservedAt <= cooldownThreshold);

        if (!string.IsNullOrWhiteSpace(externalChatId))
        {
            candidatesQuery = candidatesQuery.Where(item => item.SourceRoom == externalChatId);
        }

        var candidates = await candidatesQuery
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Loaded work item auto-resolution candidates. CandidateCount={CandidateCount}, ExternalChatId={ExternalChatId}.",
            candidates.Count,
            externalChatId ?? "(all)");

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
        foreach (var item in candidates)
        {
            var roomMessages = messagesByRoom.GetValueOrDefault(item.SourceRoom);
            if (roomMessages is null || roomMessages.Count == 0)
            {
                continue;
            }

            var laterMessages = roomMessages
                .Where(message => IsLaterThanItem(message, item))
                .ToList();

            var resolution = WorkItemAutoResolutionDetector.TryResolve(item, laterMessages);
            if (resolution is null)
            {
                continue;
            }

            item.ResolvedAt = resolution.ResolvedAt;
            item.ResolutionKind = resolution.ResolutionKind;
            item.ResolutionSource = resolution.ResolutionSource;
            item.UpdatedAt = resolution.ResolvedAt;
            changed = true;
            resolvedCount++;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Completed work item auto-resolution. CandidateCount={CandidateCount}, ResolvedCount={ResolvedCount}, MessageCount={MessageCount}.",
            candidates.Count,
            resolvedCount,
            messages.Count);
    }

    private static bool IsLaterThanItem(NormalizedMessageEntity message, WorkItemEntity item)
    {
        if (message.SentAt > item.ObservedAt)
        {
            return true;
        }

        return message.SentAt == item.ObservedAt &&
               !string.Equals(message.ExternalMessageId, item.SourceEventId, StringComparison.Ordinal);
    }
}
