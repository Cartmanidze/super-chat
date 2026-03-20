using Microsoft.Extensions.Logging;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal static class WorkItemTimeZoneResolver
{
    public static TimeZoneInfo Resolve(ILogger logger, string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException ex)
            {
                logger.LogWarning(ex, "Configured Today time zone '{TimeZoneId}' was not found. Falling back to UTC.", configuredTimeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                logger.LogWarning(ex, "Configured Today time zone '{TimeZoneId}' is invalid. Falling back to UTC.", configuredTimeZoneId);
            }
        }

        return TimeZoneInfo.Utc;
    }
}
