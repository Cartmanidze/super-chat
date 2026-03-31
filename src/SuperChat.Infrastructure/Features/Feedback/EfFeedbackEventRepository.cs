using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Feedback;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Feedback;

internal sealed class EfFeedbackEventRepository(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
    : EfCoreRepository<FeedbackEventEntity>(dbContextFactory), IFeedbackEventRepository
{
    public async Task AddAsync(FeedbackEvent feedback, CancellationToken cancellationToken)
    {
        await using var db = await GetDbContextAsync(cancellationToken);
        db.FeedbackEvents.Add(new FeedbackEventEntity
        {
            Id = feedback.Id,
            UserId = feedback.UserId,
            Area = feedback.Area,
            Value = feedback.Value,
            Notes = feedback.Notes,
            CreatedAt = feedback.CreatedAt
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
