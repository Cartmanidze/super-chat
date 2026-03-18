namespace SuperChat.Infrastructure.Services;

internal static class WorkItemActionKey
{
    private const string ExtractedPrefix = "extracted";
    private const string MeetingPrefix = "meeting";

    public static string ForExtractedItem(Guid id) => $"{ExtractedPrefix}:{id:N}";

    public static string ForMeeting(Guid id) => $"{MeetingPrefix}:{id:N}";

    public static bool TryParse(string? value, out WorkItemActionTarget target, out Guid id)
    {
        target = default;
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out id))
        {
            return false;
        }

        target = parts[0].ToLowerInvariant() switch
        {
            ExtractedPrefix => WorkItemActionTarget.ExtractedItem,
            MeetingPrefix => WorkItemActionTarget.Meeting,
            _ => default
        };

        return target is not WorkItemActionTarget.Unknown;
    }
}

internal enum WorkItemActionTarget
{
    Unknown = 0,
    ExtractedItem = 1,
    Meeting = 2
}
