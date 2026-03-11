using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class ExtractedItemService(IDbContextFactory<SuperChatDbContext> dbContextFactory) : IExtractedItemService
{
    public async Task AddRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var entities = items
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
    }

    public async Task<IReadOnlyList<ExtractedItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.ExtractedItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.ObservedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(item => item.ToDomain()).ToList();
    }
}
