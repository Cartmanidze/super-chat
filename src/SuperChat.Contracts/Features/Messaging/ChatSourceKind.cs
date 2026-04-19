namespace SuperChat.Contracts.Features.Messaging;

public enum ChatSourceKind
{
    Telegram = 1,
    Max = 2
}

public static class ChatSourceKindExtensions
{
    public static string ToSourceLabel(this ChatSourceKind kind)
    {
        return kind switch
        {
            ChatSourceKind.Telegram => "telegram",
            ChatSourceKind.Max => "max",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown chat source.")
        };
    }
}
