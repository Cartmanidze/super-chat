namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class TelegramSessionEntity
{
    public Guid UserId { get; set; }
    public byte[] AuthKeyEncrypted { get; set; } = [];
    public int DcId { get; set; }
    public string ServerAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
