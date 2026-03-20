using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record ConversationWindow(
    Guid UserId,
    string Source,
    string MatrixRoomId,
    IReadOnlyList<NormalizedMessage> Messages)
{
    public NormalizedMessage FirstMessage => Messages[0];

    public NormalizedMessage LastMessage => Messages[^1];

    public DateTimeOffset TsFrom => FirstMessage.SentAt;

    public DateTimeOffset TsTo => LastMessage.SentAt;

    public string Transcript => string.Join('\n', Messages.Select(RenderMessageLine));

    private static string RenderMessageLine(NormalizedMessage message)
    {
        var sender = string.IsNullOrWhiteSpace(message.SenderName)
            ? "Unknown"
            : message.SenderName.Trim();
        var text = string.IsNullOrWhiteSpace(message.Text)
            ? string.Empty
            : message.Text.Trim();

        return $"{sender}: {text}";
    }
}
