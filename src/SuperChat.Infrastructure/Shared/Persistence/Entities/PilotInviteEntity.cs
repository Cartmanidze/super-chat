namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class PilotInviteEntity
{
    public string Email { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public DateTimeOffset InvitedAt { get; set; }
    public bool IsActive { get; set; }
}
