namespace SuperChat.Infrastructure.Shared.Persistence;

internal static class LikePatternEscaper
{
    public const string EscapeCharacter = "\\";

    public static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    public static string ToContainsPattern(string value)
    {
        return "%" + Escape(value) + "%";
    }
}
