using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class FeedbackService(IDbContextFactory<SuperChatDbContext> dbContextFactory) : IFeedbackService
{
    public async Task RecordAsync(Guid userId, string area, bool useful, string? note, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.FeedbackEvents.Add(new FeedbackEventEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Area = area,
            Value = useful ? "useful" : "not_useful",
            Notes = note,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
