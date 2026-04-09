namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

public interface IUserTimeZoneResolver
{
    ValueTask<TimeZoneInfo> ResolveAsync(Guid userId, TimeZoneInfo fallbackTimeZone, CancellationToken cancellationToken);
}
