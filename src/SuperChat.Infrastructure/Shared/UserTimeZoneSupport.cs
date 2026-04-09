namespace SuperChat.Infrastructure.Shared;

internal static class UserTimeZoneSupport
{
    public static string? NormalizeTimeZoneId(string? timeZoneId)
    {
        var timeZone = TryResolve(timeZoneId);
        return timeZone?.Id;
    }

    public static TimeZoneInfo ResolveOrFallback(string? timeZoneId, TimeZoneInfo fallbackTimeZone)
    {
        return TryResolve(timeZoneId) ?? fallbackTimeZone;
    }

    private static TimeZoneInfo? TryResolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
