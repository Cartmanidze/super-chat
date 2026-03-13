namespace SuperChat.Domain.Model;

public static class ChatPromptTemplate
{
    public const string Today = "today";
    public const string Waiting = "waiting";
    public const string Meetings = "meetings";
    public const string Recent = "recent";
    public const string Custom = "custom";

    public static bool IsSupported(string? value)
    {
        return string.Equals(value, Today, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Waiting, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Meetings, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Recent, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Custom, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Today
            : value.Trim().ToLowerInvariant();
    }
}
