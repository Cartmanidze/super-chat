namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class NormalizedMessageEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string MatrixRoomId { get; set; } = string.Empty;
    public string MatrixEventId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
    public bool Processed { get; set; }
}
