namespace SuperChat.Domain.Features.Chat;

public static class ChatPromptTemplate
{
    public const string Meetings = "meetings";

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Meetings
            : value.Trim().ToLowerInvariant();
    }
}
