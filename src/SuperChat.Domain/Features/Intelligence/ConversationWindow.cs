using SuperChat.Domain.Features.Messaging;
using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record ConversationWindow(
    Guid UserId,
    string Source,
    string ExternalChatId,
    IReadOnlyList<NormalizedMessage> Messages)
{
    private readonly bool _validated = Validate(UserId, Source, ExternalChatId, Messages);

    private static bool Validate(Guid userId, string source, string externalChatId, IReadOnlyList<NormalizedMessage> messages)
    {
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalChatId);
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            throw new ArgumentException("Messages must not be empty.", nameof(messages));
        return true;
    }

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
