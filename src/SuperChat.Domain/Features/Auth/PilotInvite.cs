namespace SuperChat.Domain.Model;

public sealed record PilotInvite(
    string Email,
    string InvitedBy,
    DateTimeOffset InvitedAt,
    bool IsActive);
