namespace SuperChat.Domain.Features.Auth;

public sealed record PilotInvite(
    string Email,
    string InvitedBy,
    DateTimeOffset InvitedAt,
    bool IsActive);
