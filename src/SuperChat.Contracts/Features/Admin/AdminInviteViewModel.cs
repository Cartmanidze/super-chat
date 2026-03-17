namespace SuperChat.Contracts.Features.Admin;

public sealed record AdminInviteViewModel(
    string Email,
    string InvitedBy,
    DateTimeOffset InvitedAt,
    bool IsActive);
