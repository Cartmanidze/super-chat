using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class WorkItemIngestionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMeetingService meetingService)
{
    public async Task IngestRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var filteredItems = items
            .Where(ExtractedItemFilters.ShouldKeep)
            .ToList();

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
                SourceRoom = item.SourceRoom,
                SourceEventId = item.SourceEventId,
                Person = item.Person,
                ObservedAt = item.ObservedAt,
                DueAt = item.DueAt,
                Confidence = item.Confidence,
                CreatedAt = item.ObservedAt,
                UpdatedAt = item.ObservedAt
            })
            .ToList();

        if (workItemEntities.Count > 0)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.WorkItems.AddRange(workItemEntities);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await meetingService.UpsertRangeAsync(filteredItems, cancellationToken);
    }
}
