namespace SuperChat.Infrastructure.Services;

internal static class MeetingTimeSupport
{
    public static DateTimeOffset NormalizeToUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero
            ? value
            : value.ToUniversalTime();
    }

    public static TimeZoneInfo ResolveReferenceTimeZone(string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
