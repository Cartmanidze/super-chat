using SuperChat.Domain.Features.Integrations.Telegram;

namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class TelegramConnectionEntity
{
    public Guid UserId { get; set; }
    public TelegramConnectionState State { get; set; }
    public string? WebLoginUrl { get; set; }
    public string? ManagementRoomId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? DevelopmentSeededAt { get; set; }
}
