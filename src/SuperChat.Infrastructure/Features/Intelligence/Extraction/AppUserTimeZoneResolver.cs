using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Shared;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

internal sealed class AppUserTimeZoneResolver(
    IDbContextFactory<SuperChatDbContext> dbContextFactory) : IUserTimeZoneResolver
{
    public async ValueTask<TimeZoneInfo> ResolveAsync(
        Guid userId,
        TimeZoneInfo fallbackTimeZone,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var timeZoneId = await dbContext.AppUsers
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);

        return UserTimeZoneSupport.ResolveOrFallback(timeZoneId, fallbackTimeZone);
    }
}
