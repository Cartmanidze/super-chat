using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class ExtractedItemIngestionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMeetingService meetingService)
{
    public async Task AddRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var filteredItems = items
            .Where(ExtractedItemFilters.ShouldKeep)
            .ToList();

        var entities = filteredItems
            .Select(item => new ExtractedItemEntity
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
                Confidence = item.Confidence
            })
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ExtractedItems.AddRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        await meetingService.UpsertRangeAsync(filteredItems, cancellationToken);
    }
}
