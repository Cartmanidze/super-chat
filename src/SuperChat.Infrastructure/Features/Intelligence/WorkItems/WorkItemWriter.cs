using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class WorkItemWriter(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMeetingService meetingService,
    ILogger<WorkItemWriter> logger)
{
    public async Task AcceptRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var incomingItems = items.ToList();
        var filteredItems = incomingItems
            .Where(ExtractedItemFilters.ShouldKeep)
            .ToList();

        logger.LogInformation(
            "Prepared extracted items for writing. IncomingItemCount={IncomingItemCount}, RetainedItemCount={RetainedItemCount}, ItemKinds={ItemKinds}.",
            incomingItems.Count,
            filteredItems.Count,
            MessagePipelineTrace.SummarizeKinds(filteredItems));

        if (filteredItems.Count == 0)
        {
            return;
        }

        var workItemEntities = filteredItems
            .Where(item => item.Kind is not ExtractedItemKind.Meeting)
            .Select(item => new WorkItemEntity
            {
                Id = item.Id,
                UserId = item.UserId,
                Kind = item.Kind,
                Title = item.Title,
                Summary = item.Summary,
                ExternalChatId = item.ExternalChatId,
                SourceEventId = item.SourceEventId,
                Person = item.Person,
                ObservedAt = item.ObservedAt,
                DueAt = item.DueAt,
                Confidence = item.Confidence,
                CreatedAt = item.ObservedAt,
                UpdatedAt = item.ObservedAt
            })
            .GroupBy(item => (item.UserId, item.SourceEventId, item.Kind))
            .Select(group => group
                .OrderByDescending(item => item.Confidence)
                .First())
            .ToList();

        if (workItemEntities.Count > 0)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var sourceEventIds = workItemEntities.Select(item => item.SourceEventId).Distinct().ToList();
            var userIds = workItemEntities.Select(item => item.UserId).Distinct().ToList();
            var existingKeys = (await dbContext.WorkItems
                    .Where(item => userIds.Contains(item.UserId) &&
                                   sourceEventIds.Contains(item.SourceEventId))
                    .Select(item => new { item.UserId, item.SourceEventId, item.Kind })
                    .ToListAsync(cancellationToken))
                .Select(item => $"{item.UserId:N}|{item.SourceEventId}|{item.Kind}")
                .ToHashSet(StringComparer.Ordinal);

            workItemEntities = workItemEntities
                .Where(item => !existingKeys.Contains($"{item.UserId:N}|{item.SourceEventId}|{item.Kind}"))
                .ToList();

            if (workItemEntities.Count > 0)
            {
                dbContext.WorkItems.AddRange(workItemEntities);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await meetingService.UpsertRangeAsync(filteredItems, cancellationToken);
        logger.LogInformation(
            "Completed writing extracted items. WorkItemCount={WorkItemCount}, MeetingCount={MeetingCount}.",
            workItemEntities.Count,
            filteredItems.Count(item => item.Kind == ExtractedItemKind.Meeting));
    }
}
