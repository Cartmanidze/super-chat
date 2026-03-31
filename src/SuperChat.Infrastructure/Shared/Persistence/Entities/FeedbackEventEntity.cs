namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class FeedbackEventEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
